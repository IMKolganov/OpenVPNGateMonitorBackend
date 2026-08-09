using System.Collections.Concurrent;
using System.Diagnostics;
using DataGateMonitor.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.BackgroundServices;

/// <summary>
/// Background poller for <strong>all</strong> enabled <see cref="DataGateMonitor.Models.VpnServer"/> rows (OpenVPN and Xray).
/// Interval comes from settings <c>OpenVPN_Polling_Interval</c> / <c>OpenVPN_Polling_Interval_Unit</c> (shared for every stack).
/// Set environment variable <c>OPEN_VPN_BACKGROUND_SERVICE_DISABLED=true</c> to stop polling (including Xray node sync).
/// </summary>
public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private static int _instanceCount = 0;
    private readonly ILogger<OpenVpnBackgroundService> _logger;
    private readonly VpnServerProcessorFactory _processorFactory;
    private readonly VpnServerStatusManager _statusManager;
    private readonly IStatusCacheGenerationService _statusCacheGenerationService;
    private readonly IStatusStreamLogStore _statusStreamLogStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _maxPollingDegreeOfParallelism;
    private CancellationTokenSource _delayTokenSource = new();
    private readonly ConcurrentDictionary<int, ServiceStatus> _previousStatusByServer = new();
    private DateTimeOffset _lastStatusCacheBumpUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan StatusCacheBumpMaxInterval = TimeSpan.FromMinutes(2);
    public OpenVpnBackgroundService(
        ILogger<OpenVpnBackgroundService> logger,
        IServiceProvider serviceProvider,
        VpnServerProcessorFactory processorFactory,
        VpnServerStatusManager statusManager,
        IStatusCacheGenerationService statusCacheGenerationService,
        IStatusStreamLogStore statusStreamLogStore,
        IConfiguration configuration)
    {
        _logger = logger;
        _processorFactory = processorFactory;
        _statusManager = statusManager;
        _statusCacheGenerationService = statusCacheGenerationService;
        _statusStreamLogStore = statusStreamLogStore;
        _serviceProvider = serviceProvider;
        var configuredDegree =
            configuration.GetValue<int?>("OpenVpnPolling:MaxDegreeOfParallelism")
            ?? configuration.GetValue<int?>("OPENVPN_POLLING_MAX_DEGREE_OF_PARALLELISM");
        _maxPollingDegreeOfParallelism = configuredDegree is > 0
            ? configuredDegree.Value
            : Math.Max(1, Environment.ProcessorCount);

        var newInstanceCount = Interlocked.Increment(ref _instanceCount);
        if (newInstanceCount > 1)
        {
            _logger.LogCritical($"Multiple instances detected! Total instances: {newInstanceCount}");
            throw new InvalidOperationException("Only one instance of OpenVpnBackgroundService is allowed.");
        }

        _logger.LogInformation($"OpenVpnBackgroundService instance created. Total instances: {newInstanceCount}");
        _logger.LogInformation($"Initial delay token source: {_delayTokenSource.GetHashCode()}");
    }

    public Dictionary<int, ServiceStatusDto> GetStatus() => _statusManager.GetAllStatuses();

    public async Task RunNow(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual trigger received. Cancelling wait...");
        _logger.LogInformation($"Current delay token before cancel: {_delayTokenSource.GetHashCode()}");

        if (!_delayTokenSource.IsCancellationRequested)
        {
            await _delayTokenSource.CancelAsync();
        }

        _logger.LogInformation("Resetting delay token source to allow immediate execution...");
        _delayTokenSource.Dispose();
        _delayTokenSource = new CancellationTokenSource();
        _logger.LogInformation($"New delay token source: {_delayTokenSource.GetHashCode()}");
    }

    private async Task RunOpenVpnTask(int nextRunSeconds, CancellationToken cancellationToken)
    {
        try
        {
            var cycleStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting VPN servers polling task...");
            await AppendOperationalLogAsync(
                eventType: "cycle-start",
                level: "info",
                message: "Started VPN polling cycle.",
                ct: cancellationToken);
            using var scope = _serviceProvider.CreateScope();
            var openVpnServerQueryService = scope.ServiceProvider.GetRequiredService<IVpnServerQueryService>();
            var openVpnServers = await openVpnServerQueryService.GetAll(ct: cancellationToken);
            _statusManager.ClearAllStatuses();
            var totalServers = openVpnServers.Count;
            var disabledServers = openVpnServers.Count(s => s.IsDisable);
            var processedServers = 0;
            var successServers = 0;
            var timeoutServers = 0;
            var failedServers = 0;
            var currentInFlight = 0;
            var maxObservedInFlight = 0;
            var serverDurations = new ConcurrentBag<long>();

            // Disabled rows are never polled — still publish Idle so the status stream is not empty and
            // clients do not treat "missing server" as Pending for the whole fleet.
            // Also clear hanging IsConnected sessions (disable may have been set outside UpdateVpnServer).
            var presence = scope.ServiceProvider.GetRequiredService<IVpnServerClientPresenceService>();
            foreach (var skipped in openVpnServers.Where(s => s.IsDisable))
            {
                _statusManager.UpdateStatus(skipped.Id, ServiceStatus.Idle, nextRunSeconds);
                await presence.MarkAllDisconnectedAsync(skipped.Id, cancellationToken);
            }

            var serversToPoll = openVpnServers.Where(x => x.IsDisable != true).ToList();
            _logger.LogInformation(
                "VPN polling cycle plan: total={TotalServers}, disabled={DisabledServers}, toPoll={ServersToPoll}.",
                totalServers,
                disabledServers,
                serversToPoll.Count);
            await AppendOperationalLogAsync(
                eventType: "cycle-plan",
                level: "info",
                message: $"Planned polling for {serversToPoll.Count} servers ({disabledServers} disabled).",
                ct: cancellationToken,
                metrics: new
                {
                    totalServers,
                    disabledServers,
                    toPollServers = serversToPoll.Count,
                    configuredMaxParallelism = _maxPollingDegreeOfParallelism,
                    processorCount = Environment.ProcessorCount
                });

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _maxPollingDegreeOfParallelism
            };

            await Parallel.ForEachAsync(serversToPoll, parallelOptions, async (server, ct) =>
            {
                Interlocked.Increment(ref processedServers);
                var inFlight = Interlocked.Increment(ref currentInFlight);
                UpdateMaxValue(ref maxObservedInFlight, inFlight);
                var serverStopwatch = Stopwatch.StartNew();
                _logger.LogInformation(
                    $"VpnServerId: {server.Id}. VpnServerName: {server.ServerName} Processing server: {server.ApiUrl}");
                await AppendOperationalLogAsync(
                    eventType: "server-start",
                    level: "info",
                    message: $"Started polling server {server.ServerName}.",
                    ct: ct,
                    serverId: server.Id,
                    serverName: server.ServerName,
                    apiUrl: server.ApiUrl,
                    serverType: server.ServerType,
                    metrics: new
                    {
                        inFlight,
                        managedThreadId = Environment.CurrentManagedThreadId
                    });

                try
                {
                    _statusManager.UpdateStatus(server.Id, ServiceStatus.Running, nextRunSeconds);

                    var processor = _processorFactory.GetOrCreateProcessor(server);
                    await processor.ProcessServerAsync(server, ct);

                    _statusManager.UpdateStatus(server.Id, ServiceStatus.Idle, nextRunSeconds);
                    Interlocked.Increment(ref successServers);
                    if (_previousStatusByServer.TryGetValue(server.Id, out var prevStatus)
                        && prevStatus == ServiceStatus.Error)
                    {
                        await SafeNotifyAsync(
                            async notifySvc =>
                                await notifySvc.NotifyBecameAvailable(server.Id, server.ServerName, ct),
                            server.Id,
                            server.ServerName);
                    }

                    _logger.LogInformation(
                        $"VpnServerId: {server.Id}. VpnServerName: {server.ServerName} " +
                        $"Completed processing for server Id: {server.Id} Name: {server.ServerName}. " +
                        $"Duration: {serverStopwatch.ElapsedMilliseconds} ms");
                    serverDurations.Add(serverStopwatch.ElapsedMilliseconds);
                    await AppendOperationalLogAsync(
                        eventType: "server-success",
                        level: "info",
                        message: $"Server {server.ServerName} polled successfully.",
                        ct: ct,
                        serverId: server.Id,
                        serverName: server.ServerName,
                        apiUrl: server.ApiUrl,
                        serverType: server.ServerType,
                        durationMs: serverStopwatch.ElapsedMilliseconds);
                }
                catch (TimeoutException ex)
                {
                    _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, "Timeout");
                    Interlocked.Increment(ref timeoutServers);

                    _logger.LogError(
                        ex,
                        $"VpnServerId: {server.Id}. VpnServerName: {server.ServerName} " +
                        $"Timeout while processing VPN server {server.ApiUrl}. " +
                        $"Duration before timeout: {serverStopwatch.ElapsedMilliseconds} ms");
                    await AppendOperationalLogAsync(
                        eventType: "server-timeout",
                        level: "error",
                        message: $"Timeout while polling server {server.ServerName}.",
                        ct: ct,
                        serverId: server.Id,
                        serverName: server.ServerName,
                        apiUrl: server.ApiUrl,
                        serverType: server.ServerType,
                        durationMs: serverStopwatch.ElapsedMilliseconds,
                        details: ex.Message);

                    await SafeNotifyAsync(
                        async notifySvc => 
                            await notifySvc.NotifyNoResponseFromServer(server.Id, server.ServerName, ct),
                        server.Id,
                        server.ServerName);
                }
                catch (Exception ex)
                {
                    var errorDetails = GetExceptionDetails(ex);

                    _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, errorDetails);
                    Interlocked.Increment(ref failedServers);

                    _logger.LogError(
                        ex,
                        $"VpnServerId: {server.Id}. VpnServerName: {server.ServerName} " +
                        $"Error processing VPN server {server.ApiUrl}. Details: {errorDetails}. " +
                        $"Duration before failure: {serverStopwatch.ElapsedMilliseconds} ms");
                    await AppendOperationalLogAsync(
                        eventType: "server-error",
                        level: "error",
                        message: $"Failed to poll server {server.ServerName}.",
                        ct: ct,
                        serverId: server.Id,
                        serverName: server.ServerName,
                        apiUrl: server.ApiUrl,
                        serverType: server.ServerType,
                        durationMs: serverStopwatch.ElapsedMilliseconds,
                        details: errorDetails);

                    await SafeNotifyAsync(
                        async notifySvc => await notifySvc.NotifyBecameUnavailableDueToError(
                            server.Id,
                            server.ServerName,
                            errorDetails,
                            ct),
                        server.Id,
                        server.ServerName);
                }
                finally
                {
                    Interlocked.Decrement(ref currentInFlight);
                }
            });

            var statusChanged = false;
            foreach (var (serverId, dto) in _statusManager.GetAllStatuses())
            {
                _previousStatusByServer.AddOrUpdate(
                    serverId,
                    _ =>
                    {
                        statusChanged = true;
                        return dto.Status;
                    },
                    (_, previous) =>
                    {
                        if (previous != dto.Status)
                            statusChanged = true;
                        return dto.Status;
                    });
            }

            // Avoid invalidating get-all-with-status on every poll; refresh at least every 2 minutes
            // so CountSessions stays reasonably fresh while Redis overlays keep connected counts live.
            var nowUtc = DateTimeOffset.UtcNow;
            if (statusChanged || nowUtc - _lastStatusCacheBumpUtc >= StatusCacheBumpMaxInterval)
            {
                _statusCacheGenerationService.Bump();
                _lastStatusCacheBumpUtc = nowUtc;
            }

            _logger.LogInformation(
                "VPN polling cycle completed in {ElapsedMs} ms. Processed={Processed}, Success={Success}, Timeouts={Timeouts}, Failed={Failed}, Disabled={Disabled}.",
                cycleStopwatch.ElapsedMilliseconds,
                processedServers,
                successServers,
                timeoutServers,
                failedServers,
                disabledServers);
            var avgServerDurationMs = serverDurations.Count > 0
                ? (long)Math.Round(serverDurations.Average())
                : 0;
            var maxServerDurationMs = serverDurations.Count > 0
                ? serverDurations.Max()
                : 0;
            await AppendOperationalLogAsync(
                eventType: "cycle-completed",
                level: "info",
                message: "VPN polling cycle completed.",
                ct: cancellationToken,
                durationMs: cycleStopwatch.ElapsedMilliseconds,
                metrics: new
                {
                    totalServers,
                    disabledServers,
                    processedServers,
                    successServers,
                    timeoutServers,
                    failedServers,
                    configuredMaxParallelism = _maxPollingDegreeOfParallelism,
                    observedMaxParallelism = maxObservedInFlight,
                    processorCount = Environment.ProcessorCount,
                    avgServerDurationMs,
                    maxServerDurationMs
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during VPN servers polling task. Retrying after short delay.");
            await AppendOperationalLogAsync(
                eventType: "cycle-failed",
                level: "error",
                message: "VPN polling cycle failed and will retry after short delay.",
                ct: cancellationToken,
                details: GetExceptionDetails(ex));
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var isDisabled = string.Equals(
            Environment.GetEnvironmentVariable("OPEN_VPN_BACKGROUND_SERVICE_DISABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (isDisabled)
        {
            return;
        }

        _logger.LogInformation("VPN servers background poller: execution started (OpenVPN + Xray).");

        // First cycle only: give VPN sidecars / microservices time to listen (Compose start order).
        var startupDelaySeconds = 5;
        var delayEnv = Environment.GetEnvironmentVariable("BACKGROUND_SERVICE_STARTUP_DELAY_SECONDS");
        if (!string.IsNullOrWhiteSpace(delayEnv) && int.TryParse(delayEnv, out var parsed) && parsed >= 0)
        {
            startupDelaySeconds = parsed;
        }

        if (startupDelaySeconds > 0)
        {
            _logger.LogInformation(
                "VPN poller: waiting {Seconds}s before first sync (set BACKGROUND_SERVICE_STARTUP_DELAY_SECONDS=0 to skip).",
                startupDelaySeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        var nextRunSeconds = await GetPollingIntervalSecondsAsync(cancellationToken);
        await RunOpenVpnTask(nextRunSeconds, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            nextRunSeconds = await GetPollingIntervalSecondsAsync(cancellationToken);

            if (nextRunSeconds == 0)
            {
                _logger.LogWarning("VPN servers poller: polling interval is 0. Pausing execution...");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("VPN servers poller: cancellation requested. Exiting service.");
                    return;
                }
            }

            var statuses = _statusManager.GetAllStatuses().Values.ToList();
            var nextRunTime = statuses.Any()
                ? statuses.Select(status => status.NextRunTime).Min()
                : DateTimeOffset.UtcNow.AddSeconds(120);

            var now = DateTimeOffset.UtcNow;

            if (now < nextRunTime)
            {
                var waitTime = (nextRunTime - now).TotalMilliseconds;

                _logger.LogInformation(
                    $"VPN servers poller: waiting {waitTime / 1000:F0} seconds until next run at {nextRunTime}");
                _logger.LogInformation(
                    $"VPN servers poller: delay token before waiting: {_delayTokenSource.GetHashCode()}");

                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _delayTokenSource.Token);

                    await Task.Delay(TimeSpan.FromMilliseconds(waitTime), linkedCts.Token);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("VPN servers poller: manual trigger received. Skipping wait.");
                    _logger.LogInformation(
                        $"VPN servers poller: is cancellation requested: " +
                        $"{cancellationToken.IsCancellationRequested}");
                }
            }

            _logger.LogInformation("VPN servers background poller: running sync cycle.");
            await RunOpenVpnTask(nextRunSeconds, cancellationToken);
        }
    }

    private async Task<int> GetPollingIntervalSecondsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            return await GetPollingIntervalSecondsAsync(settingsService, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Cancelling polling interval due to error: {ex}");
        }

        return 0;
    }

    private async Task<int> GetPollingIntervalSecondsAsync(ISettingsService settingsService, CancellationToken ct)
    {
        var interval = await settingsService.GetValueAsync<int>("OpenVPN_Polling_Interval", ct);
        var unit = await settingsService.GetValueAsync<string>("OpenVPN_Polling_Interval_Unit", ct);
        unit ??= "seconds";

        return unit.ToLower() switch
        {
            "minutes" => interval * 60,
            _ => interval
        };
    }

    private async Task SafeNotifyAsync(
        Func<IServerOpenVpnNotificationService, Task> notifyAction,
        int serverId,
        string serverName)
    {
        try
        {
            using var notifyScope = _serviceProvider.CreateScope();
            var notifySvc = notifyScope.ServiceProvider.GetRequiredService<IServerOpenVpnNotificationService>();
            await notifyAction(notifySvc);
        }
        catch (Exception notifyEx)
        {
            _logger.LogError(
                notifyEx,
                $"VpnServerId: {serverId}. VpnServerName: {serverName}. Notification sending failed.");
        }
    }

    private async Task AppendOperationalLogAsync(
        string eventType,
        string level,
        string message,
        CancellationToken ct,
        int? serverId = null,
        string? serverName = null,
        string? apiUrl = null,
        VpnServerType? serverType = null,
        long? durationMs = null,
        string? details = null,
        object? metrics = null)
    {
        try
        {
            var payload = new
            {
                kind = "polling-event",
                eventType,
                level,
                message,
                serverId,
                serverName,
                apiUrl,
                serverType = serverType.HasValue ? (int)serverType.Value : (int?)null,
                durationMs,
                details,
                metrics
            };

            await _statusStreamLogStore.AppendAsync(
                new StatusStreamLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    PayloadJson = ProjectJson.Serialize(payload),
                    Source = "service"
                },
                ct);
        }
        catch (OperationCanceledException)
        {
            // normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append operational polling log entry.");
        }
    }

    private static void UpdateMaxValue(ref int target, int candidate)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref target);
            if (candidate <= snapshot)
                return;
            if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot)
                return;
        }
    }

    private static string GetExceptionDetails(Exception ex)
    {
        if (ex is DbUpdateException dbEx)
        {
            if (dbEx.InnerException is PostgresException pg)
            {
                return $"{pg.MessageText} (SQLSTATE {pg.SqlState})";
            }

            if (dbEx.InnerException != null)
            {
                return dbEx.InnerException.Message;
            }
        }

        var current = ex;

        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}