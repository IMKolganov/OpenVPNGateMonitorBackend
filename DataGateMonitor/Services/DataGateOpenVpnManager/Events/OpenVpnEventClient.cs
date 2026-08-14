using System.Diagnostics;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy.Hubs;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy.Hubs.Interfaces;
using DataGateMonitor.Services.Others.Notifications.OpenVpnMicroserviceClient;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.VpnEvent.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerEvent.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerEvent.Responses;

namespace DataGateMonitor.Services.DataGateOpenVpnManager.Events;

public class OpenVpnEventClient(
    VpnServer openVpnServer,
    ILogger<OpenVpnEventClient> logger,
    IHubContext<OpenVpnEventHub> eventHub,
    IMicroserviceTokenService tokenService,
    IServiceScopeFactory scopeFactory,
    IEventHubConnectionFactory? eventHubConnectionFactory = null,
    TimeSpan? startRetryDelay = null,
    Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
{
    private readonly VpnServer _openVpnServer = openVpnServer;
    private readonly IEventHubConnectionFactory _eventHubFactory =
        eventHubConnectionFactory ?? new DefaultEventHubConnectionFactory();
    private readonly TimeSpan _startRetryDelay =
        startRetryDelay ?? OpenVpnHubConnectionDefaults.StartFailureRetryDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _retryDelayAsync =
        retryDelayAsync ?? ((delay, ct) => Task.Delay(delay, ct));

    public string RegisteredApiUrl { get; } = openVpnServer.ApiUrl;

    private IHubConnectionProxy? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _handlersRegistered;

    // ---- diagnostics ----
    private string _fullUrl = "";
    private string _host = "";
    private int _port;
    private HubConnectionState _lastState = HubConnectionState.Disconnected;
    private DateTimeOffset _lastStateChangedUtc = DateTimeOffset.MinValue;
    private DateTimeOffset? _lastReconnectedUtc;
    private DateTimeOffset? _lastClosedUtc;
    private string? _lastError;
    private bool _notifiedEventHubConnectionFailed;

    public virtual async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);
        // var s = GetStatus();
        // logger.LogInformation(
        //     "OpenVpnEventClient started. Status={State}, ConnId={ConnId}, Url={Url}, Host={Host}, Port={Port}",
        //     s.State, s.ConnectionId, s.Url, s.Host, s.Port);
    }

    /// <summary>Stops and disposes the SignalR connection (e.g. when server is updated and client is removed from cache).</summary>
    public async Task StopAsync()
    {
        await _connectionLock.WaitAsync(CancellationToken.None);
        try
        {
            if (_connection is not null)
            {
                await DisposeConnectionAsync();
                logger.LogInformation("Stopped OpenVpnEventClient for server {ServerId}", _openVpnServer.Id);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public ConnectionStatusResponse GetStatus()
    {
        return new ConnectionStatusResponse()
        {
            ConnectionStatus = new ConnectionStatusDto()
            {
                ServerId = _openVpnServer.Id,
                Url = _fullUrl,
                Host = _host,
                Port = _port,
                State = _lastState.ToString(),
                ConnectionId = _connection?.ConnectionId,
                LastStateChangedUtc = _lastStateChangedUtc,
                LastReconnectedUtc = _lastReconnectedUtc,
                LastClosedUtc = _lastClosedUtc,
                LastError = _lastError
            }
        };
    }

    private void InitTargetUrl()
    {
        if (!string.IsNullOrEmpty(_fullUrl)) return;
        _fullUrl = $"{_openVpnServer.ApiUrl.TrimEnd('/')}/hubs/openvpn-event";
        var uri = new Uri(_fullUrl);
        _host = uri.Host;
        _port = uri.IsDefaultPort ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80) : uri.Port;
    }

    private void Stamp(HubConnectionState state, Exception? error = null)
    {
        _lastState = state;
        _lastStateChangedUtc = DateTimeOffset.UtcNow;
        _lastError = error?.Message;
        if (state == HubConnectionState.Connected) _lastReconnectedUtc = _lastStateChangedUtc;
        if (error != null) _lastClosedUtc = _lastStateChangedUtc;
    }

    private async Task<IHubConnectionProxy> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null
                && !VpnServerApiUrlNormalizer.Equals(_openVpnServer.ApiUrl, RegisteredApiUrl))
            {
                logger.LogWarning(
                    "Detected API URL change for server {ServerId}. Recreating SignalR event connection...",
                    _openVpnServer.Id);
                logger.LogDebug(
                    "Event hub URL change for server {ServerId}: RegisteredApiUrl={RegisteredApiUrl}, CurrentApiUrl={CurrentApiUrl}",
                    _openVpnServer.Id, RegisteredApiUrl, _openVpnServer.ApiUrl);
                await DisposeConnectionAsync();
                _fullUrl = "";
            }

            if (_connection is not null && _connection.State == HubConnectionState.Connected)
            {
                logger.LogDebug(
                    "Event hub already Connected for server {ServerId}, ConnId={ConnId}",
                    _openVpnServer.Id, _connection.ConnectionId);
                return _connection;
            }

            if (_connection == null)
            {
                InitTargetUrl();
                logger.LogInformation(
                    "Creating SignalR connection for server {ServerId} (Url={Url}, Host={Host}, Port={Port})",
                    _openVpnServer.Id, _fullUrl, _host, _port);

                _connection = _eventHubFactory.Create(
                    _fullUrl,
                    () => Task.FromResult<string?>(tokenService.GenerateToken(
                        "vpn-cert-issuer", "cert-create", "backend", "DataGateOpenVpnManager")));

                if (!_handlersRegistered)
                {
                    _connection.On<VpnEventRequest>("ClientConnected",
                        async data => await HandleEvent("ClientConnected", data));
                    _connection.On<VpnEventRequest>("ClientDisconnected",
                        async data => await HandleEvent("ClientDisconnected", data));
                    _connection.On<VpnEventRequest>("ClientAttempted",
                        async data => await HandleEvent("ClientAttempted", data));
                    _connection.On<VpnEventRequest>("TlsVerified",
                        async data => await HandleEvent("TlsVerified", data));
                    _connection.On<VpnEventRequest>("ErrorEvent",
                        async data => await HandleEvent("ErrorEvent", data));
                    _connection.On<VpnEventRequest>("AuthFailed",
                        async data => await HandleEvent("AuthFailed", data));
                    _connection.On<VpnEventRequest>("TlsError",
                        async data => await HandleEvent("TlsError", data));
                    _connection.On<VpnEventRequest>("VerifyError",
                        async data => await HandleEvent("VerifyError", data));
                    _connection.On<VpnEventRequest>("VpnError",
                        async data => await HandleEvent("VpnError", data));
                    _connection.On<DnsQueryBatchRequest>("DnsQueriesReceived",
                        async data => await HandleDnsQueriesAsync(data));
                    _connection.On<object>("EnvDumpReceived",
                        async _ => await Task.CompletedTask);

                    _connection.OnReconnecting(ex =>
                    {
                        Stamp(HubConnectionState.Reconnecting, ex);
                        logger.LogDebug(
                            ex,
                            "SignalR Reconnecting event (ServerId={ServerId}, State={State}, ConnId={ConnId})",
                            _openVpnServer.Id, _connection?.State, _connection?.ConnectionId);
                        logger.LogWarning(ex,
                            "SignalR reconnecting (ServerId={ServerId}, Host={Host}, Port={Port})",
                            _openVpnServer.Id, _host, _port);
                        return Task.CompletedTask;
                    });
                    _connection.OnReconnected(connId =>
                    {
                        Stamp(HubConnectionState.Connected);
                        logger.LogDebug(
                            "SignalR Reconnected event (ServerId={ServerId}, ConnId={ConnId}, State={State})",
                            _openVpnServer.Id, connId, _connection?.State);
                        logger.LogInformation(
                            "SignalR reconnected (ServerId={ServerId}, ConnId={ConnId}, Host={Host}, Port={Port})",
                            _openVpnServer.Id, connId, _host, _port);
                        return Task.CompletedTask;
                    });
                    _connection.OnClosed(ex =>
                    {
                        Stamp(HubConnectionState.Disconnected, ex);
                        logger.LogDebug(
                            ex,
                            "SignalR Closed event (ServerId={ServerId}, State={State}) — manual StartAsync required after auto-reconnect gives up",
                            _openVpnServer.Id, _connection?.State);
                        logger.LogError(ex,
                            "SignalR closed (ServerId={ServerId}, Host={Host}, Port={Port})",
                            _openVpnServer.Id, _host, _port);
                        return Task.CompletedTask;
                    });

                    _handlersRegistered = true;
                }
            }

            if (_connection.State != HubConnectionState.Connected)
            {
                var attempt = 0;
                while (_connection.State != HubConnectionState.Connected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    attempt++;

                    try
                    {
                        logger.LogInformation(
                            "Attempting SignalR connect: ServerId={ServerId}, Attempt={Attempt}, Url={Url}, Host={Host}, Port={Port}",
                            _openVpnServer.Id, attempt, _fullUrl, _host, _port);
                        logger.LogDebug(
                            "Event hub StartWhenReady: ServerId={ServerId}, Attempt={Attempt}, CurrentState={State}",
                            _openVpnServer.Id, attempt, _connection.State);
                        await HubConnectionStartup.StartWhenReadyAsync(
                            () => _connection.State,
                            ct => _connection.StartAsync(ct),
                            cancellationToken,
                            logger: logger);
                        _notifiedEventHubConnectionFailed = false;
                        Stamp(HubConnectionState.Connected);
                        logger.LogInformation(
                            "Started OpenVpnEventClient SignalR connection for server {ServerId}. ConnId={ConnId}, Host={Host}, Port={Port}",
                            _openVpnServer.Id, _connection.ConnectionId, _host, _port);
                        break;
                    }
                    catch (Exception ex)
                    {
                        var innerMsg = ex.InnerException?.Message ?? ex.Message;
                        logger.LogWarning(ex,
                            "SignalR start failed (attempt {Attempt}) for server {ServerId}, Url={Url}. Inner={Inner}. Retrying in {RetryDelay}s...",
                            attempt, _openVpnServer.Id, _fullUrl, innerMsg, _startRetryDelay.TotalSeconds);

                        if (!_notifiedEventHubConnectionFailed)
                        {
                            _notifiedEventHubConnectionFailed = true;
                            try
                            {
                                using var notifyScope = scopeFactory.CreateScope();
                                var notifySvc = notifyScope.ServiceProvider.GetRequiredService<IOpenVpnMicroserviceNotificationService>();
                                await notifySvc.NotifyEventHubConnectionFailed(_openVpnServer.Id, _openVpnServer.ServerName, innerMsg, CancellationToken.None);
                            }
                            catch (Exception notifyEx)
                            {
                                logger.LogWarning(notifyEx, "Failed to send event-hub connection-failed notification for server {ServerId}", _openVpnServer.Id);
                            }
                        }

                        await _retryDelayAsync(_startRetryDelay, cancellationToken);
                    }
                }
            }

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
            return;

        try { await _connection.StopAsync(CancellationToken.None); } catch { /* ignore */ }
        try { await _connection.DisposeAsync(); } catch { /* ignore */ }
        _connection = null;
        _handlersRegistered = false;
    }

    private async Task HandleEvent(string eventType, VpnEventRequest data)
    {
        var swTotal = Stopwatch.StartNew();
        var group = _openVpnServer.Id.ToString();

        try
        {
            logger.LogInformation(
                "Handling event {EventType} for ServerId={ServerId}; CN={CommonName}; Real={Real}; Virt={Virt}; Since={Since}",
                eventType, _openVpnServer.Id, data.CommonName, data.RealAddress, data.VirtualAddress,
                data.ConnectedSince);

            using var scope = scopeFactory.CreateScope();

            var logService = scope.ServiceProvider.GetRequiredService<IVpnEventLogService>();
            var fileQuery = scope.ServiceProvider.GetRequiredService<IIssuedOvpnFileQueryService>();
            var proxyLookup = scope.ServiceProvider.GetRequiredService<IProxyClientLookupService>();
            var clientUpsert = scope.ServiceProvider.GetRequiredService<IVpnServerClientUpsertService>();
            var clientCmd = scope.ServiceProvider.GetRequiredService<ICommandService<VpnServerClient, int>>();

            var req = data.Adapt<VpnEventRequest>();

            var swSave = Stopwatch.StartNew();
            await logService.SaveEventAsync(_openVpnServer.Id, eventType, req, CancellationToken.None);

            if (eventType.Equals("ClientConnected", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(req.CommonName))
            {
                var remoteIp = OpenVpnRealAddressParser.NormalizeRemoteIp(req.RealAddress);
                var openVpnServerClient = new VpnServerClient
                {
                    VpnServerId = _openVpnServer.Id,
                    ExternalId = await fileQuery.GetExternalIdByCommonName(
                        req.CommonName, _openVpnServer.Id, CancellationToken.None) ?? string.Empty,
                    CommonName = req.CommonName!,
                    RemoteIp = remoteIp,
                    LocalIp = req.VirtualAddress ?? string.Empty,
                    BytesReceived = req.BytesReceived ?? 0,
                    BytesSent = req.BytesSent ?? 0,
                    ConnectedSince = req.ConnectedSince ?? DateTimeOffset.UtcNow,
                    DisconnectedAt = req.DisconnectedAt,
                    Username = req.CommonName,
                    IsConnected = true,
                    LastUpdate = DateTimeOffset.UtcNow,
                    CreateDate = DateTimeOffset.UtcNow
                };

                await proxyLookup.EnrichFromManagementRealAddressAsync(_openVpnServer, openVpnServerClient,
                    CancellationToken.None);

                openVpnServerClient.SessionId = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(
                    openVpnServerClient.CommonName, openVpnServerClient.RemoteIp, openVpnServerClient.ConnectedSince);

                await clientUpsert.UpsertAsync(
                    VpnServerClientUpsertPayload.FromClient(openVpnServerClient, isConnected: true),
                    CancellationToken.None);
                logger.LogInformation("VpnServerId: {Id}. Upserted client session {SessionId}.",
                    _openVpnServer.Id, openVpnServerClient.SessionId);
            }
            else if (eventType.Equals("ClientDisconnected", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(req.CommonName))
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var remoteIp = OpenVpnRealAddressParser.NormalizeRemoteIp(req.RealAddress);
                var disconnectedAt = VpnClientDisconnectFinalizer.ResolveDisconnectedAt(req, nowUtc);
                var bytesReceived = req.BytesReceived;
                var bytesSent = req.BytesSent;

                await clientCmd.UpdateWhere(
                    x => x.VpnServerId == _openVpnServer.Id
                         && x.IsConnected
                         && x.CommonName == req.CommonName
                         && x.ConnectedSince == req.ConnectedSince
                         && x.RemoteIp == remoteIp,
                    s =>
                    {
                        s.SetProperty(c => c.IsConnected, false)
                            .SetProperty(c => c.DisconnectedAt, disconnectedAt)
                            .SetProperty(c => c.LastUpdate, nowUtc);
                        // Final session totals from client-disconnect (authoritative vs last poll).
                        if (bytesReceived is { } br)
                            s.SetProperty(c => c.BytesReceived, br);
                        if (bytesSent is { } bs)
                            s.SetProperty(c => c.BytesSent, bs);
                    },
                    CancellationToken.None);
            }

            // save while scope (DbContext) is still alive
            await clientCmd.SaveChanges(CancellationToken.None);

            swSave.Stop();
            logger.LogInformation("Saved event {EventType} for ServerId={ServerId}; SaveMs={ElapsedMs}",
                eventType, _openVpnServer.Id, swSave.ElapsedMilliseconds);

            var swHub = Stopwatch.StartNew();
            await eventHub.Clients.Group(group).SendAsync(eventType, data);
            swHub.Stop();
            logger.LogInformation("Broadcasted {EventType} to group {Group} (ServerId={ServerId}); HubMs={ElapsedMs}",
                eventType, group, _openVpnServer.Id, swHub.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to handle SignalR event {EventType} from ServerId={ServerId}; CN={CommonName}",
                eventType, _openVpnServer.Id, data.CommonName);
        }
        finally
        {
            swTotal.Stop();
            logger.LogDebug("HandleEvent finished for {EventType} (ServerId={ServerId}); TotalMs={ElapsedMs}",
                eventType, _openVpnServer.Id, swTotal.ElapsedMilliseconds);
        }
    }

    private async Task HandleDnsQueriesAsync(DnsQueryBatchRequest batch)
    {
        if (batch.Queries.Count == 0)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dnsLogService = scope.ServiceProvider.GetRequiredService<IVpnDnsQueryLogService>();
            var saved = await dnsLogService.SaveBatchAsync(_openVpnServer.Id, batch, CancellationToken.None);
            var skipped = batch.Queries.Count - saved;
            if (saved == 0)
            {
                logger.LogDebug(
                    "Pi-hole DNS batch for ServerId={ServerId}: nothing new to store ({Received} received, likely duplicates).",
                    _openVpnServer.Id,
                    batch.Queries.Count);
            }
            else
            {
                logger.LogInformation(
                    "Pi-hole DNS batch for ServerId={ServerId}: saved={Saved}, received={Received}, skippedDuplicates={Skipped}, collectedAt={CollectedAtUtc:o}",
                    _openVpnServer.Id,
                    saved,
                    batch.Queries.Count,
                    skipped,
                    batch.CollectedAtUtc);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to save Pi-hole DNS query batch for ServerId={ServerId} (received={Received}, collectedAt={CollectedAtUtc:o})",
                _openVpnServer.Id,
                batch.Queries.Count,
                batch.CollectedAtUtc);
        }
    }
}