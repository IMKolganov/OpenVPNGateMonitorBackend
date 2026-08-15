using Microsoft.AspNetCore.SignalR.Client;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;

namespace DataGateMonitor.Services.DataGateXRayManager.Events;

/// <summary>
/// Dashboard → Xray node SignalR client for Pi-hole DNS batches (<c>DnsQueriesReceived</c>).
/// Mirrors the OpenVPN event-hub receive path for DNS only.
/// </summary>
public class XrayDnsEventClient(
    VpnServer server,
    ILogger<XrayDnsEventClient> logger,
    IMicroserviceTokenService tokenService,
    IServiceScopeFactory scopeFactory)
{
    private const string AudienceXrayManager = "DataGateXRayManager";

    public string RegisteredApiUrl { get; } = server.ApiUrl;

    private HubConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _handlersRegistered;

    public virtual async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
                return;

            if (_connection is null)
            {
                var url = $"{server.ApiUrl.TrimEnd('/')}/hubs/xray-event";
                _connection = new HubConnectionBuilder()
                    .WithUrl(url, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(
                            tokenService.GenerateToken(
                                "vpn-cert-issuer", "cert-create", "backend", AudienceXrayManager));
                    })
                    .WithAutomaticReconnect()
                    .Build();

                if (!_handlersRegistered)
                {
                    _connection.On<DnsQueryBatchRequest>("DnsQueriesReceived", HandleDnsQueriesAsync);
                    _handlersRegistered = true;
                }
            }

            if (_connection.State != HubConnectionState.Connected)
            {
                await _connection.StartAsync(cancellationToken);
                logger.LogInformation(
                    "XrayDnsEventClient connected. ServerId={ServerId}, ConnId={ConnId}",
                    server.Id, _connection.ConnectionId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection is null)
                return;
            try { await _connection.StopAsync(); } catch { /* ignore */ }
            try { await _connection.DisposeAsync(); } catch { /* ignore */ }
            _connection = null;
            _handlersRegistered = false;
        }
        finally
        {
            _lock.Release();
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
            var saved = await dnsLogService.SaveBatchAsync(server.Id, batch, CancellationToken.None);
            if (saved > 0)
            {
                logger.LogInformation(
                    "Xray Pi-hole DNS batch for ServerId={ServerId}: saved={Saved}, received={Received}",
                    server.Id, saved, batch.Queries.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to save Xray Pi-hole DNS batch for ServerId={ServerId}",
                server.Id);
        }
    }
}
