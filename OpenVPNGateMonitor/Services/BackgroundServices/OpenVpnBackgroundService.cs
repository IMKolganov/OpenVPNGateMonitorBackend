using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Background;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

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
        await RunOpenVpnTask(cancellationToken);
    }

    private async Task RunOpenVpnTask(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var openVpnServers = unitOfWork.GetQuery<OpenVpnServer>().AsQueryable().ToList();//.Where(x=> x.Id == 8)

        await Parallel.ForEachAsync(openVpnServers, cancellationToken, async (server, ct) =>
        {
            try
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Running);
                var processor = _processorFactory.GetOrCreateProcessor(server);

                await processor.ProcessServerAsync(server, cancellationToken);

                _statusManager.UpdateStatus(server.Id, ServiceStatus.Idle);
            }
            catch (TimeoutException ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, "Timeout");
                _logger.LogError(ex, $"Timeout while processing OpenVPN server " +
                                     $"{server.ManagementIp}:{server.ManagementPort}");
            }
            catch (Exception ex)
            {
                _statusManager.UpdateStatus(server.Id, ServiceStatus.Error, ex.Message);
                _logger.LogError(ex, $"OpenVpnBackgroundService: Error processing OpenVPN server " +
                                     $"{server.ManagementIp}:{server.ManagementPort}");
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpenVPN Background Service started. Running initial execution...");
        await RunOpenVpnTask(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            var nextRunTime = _statusManager.GetAllStatuses()
                .Values
                .Select(status => status.NextRunTime)
                .DefaultIfEmpty(DateTime.UtcNow.AddSeconds(120))
                .Min();

            if (now < nextRunTime)
            {
                var waitTime = (nextRunTime - now).TotalMilliseconds;
                _logger.LogInformation($"Waiting {waitTime / 1000:F0} seconds until next run at {nextRunTime}");

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

            await RunOpenVpnTask(cancellationToken);
        }
    }

}
