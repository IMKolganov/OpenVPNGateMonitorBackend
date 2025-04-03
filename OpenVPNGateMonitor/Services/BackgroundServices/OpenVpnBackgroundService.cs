using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Background;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private static int _instanceCount = 0;
    private readonly ILogger<OpenVpnBackgroundService> _logger;
    private readonly OpenVpnServerProcessorFactory _processorFactory;
    private readonly OpenVpnServerStatusManager _statusManager;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource _delayTokenSource = new();

    public OpenVpnBackgroundService(
        ILogger<OpenVpnBackgroundService> logger,
        IServiceProvider serviceProvider,
        OpenVpnServerProcessorFactory processorFactory,
        OpenVpnServerStatusManager statusManager)
    {
        _logger = logger;
        _processorFactory = processorFactory;
        _statusManager = statusManager;
        _serviceProvider = serviceProvider;
        
        int newInstanceCount = Interlocked.Increment(ref _instanceCount);
        
        if (newInstanceCount > 1)
        {
            _logger.LogCritical($"Multiple instances detected! Total instances: {newInstanceCount}");
            throw new InvalidOperationException("Only one instance of OpenVpnBackgroundService is allowed.");
        }
        
        _logger.LogInformation($"OpenVpnBackgroundService instance created. Total instances: {newInstanceCount}");
        _logger.LogInformation($"Initial delay token source: {_delayTokenSource.GetHashCode()}");
    }
    
    public Dictionary<int, BackgroundServerStatus> GetStatus() => _statusManager.GetAllStatuses();

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
        _logger.LogInformation("Starting OpenVPN task execution...");
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var openVpnServers = unitOfWork.GetQuery<OpenVpnServer>().AsQueryable().ToList();
        _statusManager.ClearAllStatuses();

        await Parallel.ForEachAsync(openVpnServers, cancellationToken, async (server, ct) =>
        {
            _logger.LogInformation($"Processing server Id: {server.Id} Name: {server.ServerName} " +
                                   $"- {server.ManagementIp}:{server.ManagementPort}");
            try
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Running, nextRunSeconds);
                
                var processor = _processorFactory.GetOrCreateProcessor(server);
                await processor.ProcessServerAsync(server, ct);
                
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Idle, nextRunSeconds);
                _logger.LogInformation($"Completed processing for server Id: {server.Id} Name: {server.ServerName}");
            }
            catch (TimeoutException ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, "Timeout");
                _logger.LogError(ex, $"Timeout while processing OpenVPN server {server.ManagementIp}:{server.ManagementPort}");
            }
            catch (Exception ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, ex.Message);
                _logger.LogError(ex, $"Error processing OpenVPN server {server.ManagementIp}:{server.ManagementPort}");
            }
        });
        _logger.LogInformation("OpenVPN task execution completed.");
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpenVPN Background Service execution started.");
        var nextRunSeconds = await GetPollingIntervalSecondsAsync(cancellationToken);
        await RunOpenVpnTask(nextRunSeconds, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            nextRunSeconds = await GetPollingIntervalSecondsAsync(cancellationToken);
            if (nextRunSeconds == 0)
            {
                _logger.LogWarning("Polling interval is 0. Pausing execution...");
            
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Cancellation requested. Exiting service.");
                    return;
                }
            }
            
            var statuses = _statusManager.GetAllStatuses().Values.ToList();
            var nextRunTime = statuses.Any()
                ? statuses.Select(status => status.NextRunTime).Min()
                : DateTime.UtcNow.AddSeconds(120);

            var now = DateTime.UtcNow;
            if (now < nextRunTime)
            {
                var waitTime = (nextRunTime - now).TotalMilliseconds;
                _logger.LogInformation($"Waiting {waitTime / 1000:F0} seconds until next run at {nextRunTime}");
                _logger.LogInformation($"Delay token before waiting: {_delayTokenSource.GetHashCode()}");

                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _delayTokenSource.Token);
                    await Task.Delay(TimeSpan.FromMilliseconds(waitTime), linkedCts.Token);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Manual trigger received. Skipping wait.");
                    _logger.LogInformation("Is cancellation requested: " + cancellationToken.IsCancellationRequested);
                }
            }

            _logger.LogInformation("Executing OpenVPN task.");
            await RunOpenVpnTask(nextRunSeconds, cancellationToken);
        }
    }

    private async Task<int> GetPollingIntervalSecondsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        try
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settingsService = new SettingsService(unitOfWork);
            return await GetPollingIntervalSecondsAsync(settingsService, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Cancelling polling interval due to error: {ex}");
        }

        return 0;
    }
    
    private async Task<int> GetPollingIntervalSecondsAsync(ISettingsService settingsService, CancellationToken cancellationToken)
    {
        var interval = await settingsService.GetValueAsync<int>("OpenVPN_Polling_Interval", cancellationToken);
        var unit = await settingsService.GetValueAsync<string>("OpenVPN_Polling_Interval_Unit", cancellationToken);
        unit ??= "seconds";
        
        return unit.ToLower() switch
        {
            "minutes" => interval * 60,  
            _ => interval 
        };
    }
}