using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Background;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
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
    }

    public Dictionary<int, BackgroundServerStatus> GetStatus() => _statusManager.GetAllStatuses();

    public async Task RunNow(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual trigger received. Cancelling wait...");
        await _delayTokenSource.CancelAsync();
    }

    private async Task RunOpenVpnTask(int nextRunSeconds, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var openVpnServers = unitOfWork.GetQuery<OpenVpnServer>().AsQueryable().ToList();

        await Parallel.ForEachAsync(openVpnServers, cancellationToken, async (server, ct) =>
        {
            try
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Running, nextRunSeconds);
                var processor = _processorFactory.GetOrCreateProcessor(server);

                await processor.ProcessServerAsync(server, ct);

                _statusManager.UpdateStatus(server.Id, ServiceStatus.Idle, nextRunSeconds);
            }
            catch (TimeoutException ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, "Timeout");
                _logger.LogError(ex, $"Timeout while processing OpenVPN server " +
                                     $"{server.ManagementIp}:{server.ManagementPort}");
            }
            catch (Exception ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, nextRunSeconds, ex.Message);
                _logger.LogError(ex, $"OpenVpnBackgroundService: Error processing OpenVPN server" +
                                     $" {server.ManagementIp}:{server.ManagementPort}");
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var nextRunSeconds = 0;
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var settingsService = new SettingsService(unitOfWork);
        
        nextRunSeconds = await GetPollingIntervalSecondsAsync(settingsService, cancellationToken);
        _logger.LogInformation($"Polling interval: {nextRunSeconds} seconds");
        
        _logger.LogInformation("OpenVPN Background Service started. Running initial execution...");
        await RunOpenVpnTask(nextRunSeconds, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var statuses = _statusManager.GetAllStatuses().Values.ToList();
            var nextRunTime = statuses.Any()
                ? statuses.Select(status => status.NextRunTime).Min()
                : DateTime.UtcNow.AddSeconds(120);

            var now = DateTime.UtcNow;
            if (now < nextRunTime)
            {
                var waitTime = (nextRunTime - now).TotalMilliseconds;
                _logger.LogInformation($"Waiting {waitTime / 1000:F0} seconds until next run at {nextRunTime}");

                if (!_delayTokenSource.IsCancellationRequested)
                {
                    await _delayTokenSource.CancelAsync();
                }
                _delayTokenSource.Dispose();
                _delayTokenSource = new CancellationTokenSource();

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(waitTime), _delayTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Manual trigger received. Skipping wait.");
                }
            }

            await RunOpenVpnTask(nextRunSeconds, cancellationToken);
        }
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
