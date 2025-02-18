using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private readonly ILogger<IOpenVpnBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _seconds;
    private DateTime _nextRunTime;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private BackgroundServiceStatus _status = BackgroundServiceStatus.Idle;

    public OpenVpnBackgroundService(ILogger<IOpenVpnBackgroundService> logger, 
        IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _logger.LogInformation($"Starting background service {Guid.NewGuid()}");

        _seconds = 120; // TODO: get from app settings or configuration
        if (_seconds <= 0)
        {
            throw new InvalidOperationException("Failed to load OpenVpnSettings UpdateSecond from configuration.");
        }

        _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
    }

    public DateTime GetNextRunTime() => _nextRunTime;
    
    public BackgroundServiceStatus GetStatus() => _status;

    public async Task RunNow(CancellationToken cancellationToken)
    {
        if (!_executionLock.Wait(0)) return; // Prevent multiple executions

        try
        {
            await RunOpenVpnTask(cancellationToken);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task RunOpenVpnTask(CancellationToken cancellationToken)
    {
        if (_status == BackgroundServiceStatus.Running)
        {
            _logger.LogWarning("OpenVPN background task is already running. Skipping execution.");
            return;
        }

        _status = BackgroundServiceStatus.Running;
        _logger.LogInformation("Starting OpenVPN background task...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var openVpnServerRepository = unitOfWork.GetRepository<OpenVpnServer>();
            var openVpnServers = await openVpnServerRepository.GetAllAsync();
            var vpnServers = openVpnServers.Where(x=> x.Id == 8).ToList();

            _logger.LogInformation($"Found {vpnServers.Count} OpenVPN servers to process.");

            var maxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1);
            using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = vpnServers.Select(async openVpnServer =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    _logger.LogInformation($"Processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");

                    using var innerScope = _serviceProvider.CreateScope();
                    var innerUnitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var openVpnServerService = innerScope.ServiceProvider.GetRequiredService<IOpenVpnServerService>();
                    var innerOpenVpnServerRepository = innerUnitOfWork.GetRepository<OpenVpnServer>();

                    _logger.LogInformation($"Saving OpenVPN server status for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                    await openVpnServerService.SaveOpenVpnServerStatusLogAsync(
                        openVpnServer.Id, openVpnServer.ManagementIp, openVpnServer.ManagementPort, cancellationToken
                    );

                    _logger.LogInformation($"Saving connected clients for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                    await openVpnServerService.SaveConnectedClientsAsync(
                        openVpnServer.Id, openVpnServer.ManagementIp, openVpnServer.ManagementPort, cancellationToken
                    );

                    openVpnServer.IsOnline = true;
                    innerOpenVpnServerRepository.Update(openVpnServer);
                    await innerUnitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation($"Finished processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                }
                catch (Exception ex)
                {
                    using var errorScope = _serviceProvider.CreateScope();
                    var errorUnitOfWork = errorScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var errorRepository = errorUnitOfWork.GetRepository<OpenVpnServer>();

                    openVpnServer.IsOnline = false;
                    errorRepository.Update(openVpnServer);
                    await errorUnitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogError(ex, $"Error processing OpenVPN server {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
            _status = BackgroundServiceStatus.Idle;
            _logger.LogInformation("OpenVPN background task completed successfully.");
        }
        catch (Exception ex)
        {
            _status = BackgroundServiceStatus.Error;
            _logger.LogError(ex, "An error occurred while executing OpenVPN background task.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("OpenVPN Background Service is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                await RunOpenVpnTask(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken);
            }

            _logger.LogInformation("OpenVPN Background Service is stopping.");
        }
        catch (Exception ex)
        {
            _status = BackgroundServiceStatus.Error;
            _logger.LogError(ex, "An error occurred while running the background service.");
        }
    }
}
