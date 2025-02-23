using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private readonly ILogger<IOpenVpnBackgroundService> _logger;
    private readonly ICommandQueueManager _commandQueueManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _seconds;
    private DateTime _nextRunTime;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private BackgroundServiceStatus _status = new();
    private CancellationTokenSource _delayTokenSource = new();


    public OpenVpnBackgroundService(ILogger<IOpenVpnBackgroundService> logger, 
        IServiceProvider serviceProvider, ICommandQueueManager commandQueueManager, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _commandQueueManager = commandQueueManager;
        _logger.LogInformation($"Starting background service {Guid.NewGuid()}");

        _seconds = 120; // TODO: get from app settings or configuration
        if (_seconds <= 0)
        {
            throw new InvalidOperationException("Failed to load OpenVpnSettings UpdateSecond from configuration.");
        }

        _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
    }

    public DateTime GetNextRunTime() => _nextRunTime;

    public BackgroundServiceStatus GetStatus()
    {
        return _status;
    }

    public async Task RunNow(CancellationToken cancellationToken)
    {
        if (!await _executionLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("⚠ OpenVPN task is already running. Skipping manual execution.");
            return;
        }

        try
        {
            _logger.LogInformation("🚀 Manual trigger received. Cancelling wait...");
            await _delayTokenSource.CancelAsync(); 
            
            _logger.LogInformation("🔄 Manually triggering OpenVPN task...");
            await RunOpenVpnTask(cancellationToken);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private async Task RunOpenVpnTask(CancellationToken cancellationToken)
    {
        if (_status.ServiceStatus == ServiceStatus.Running)
        {
            _logger.LogWarning("⚠ OpenVPN background task is already running. Skipping execution.");
            return;
        }

        _status.ServiceStatus = ServiceStatus.Running;
        _logger.LogInformation("🚀 Starting OpenVPN background task...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var openVpnServers = unitOfWork.GetQuery<OpenVpnServer>().AsQueryable().ToList();

            _logger.LogInformation($"🔎 Found {openVpnServers.Count} OpenVPN servers to process.");

            var maxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1);
            using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = openVpnServers.Select(openVpnServer => Task.Run(async () =>
            {
                var commandQueue = await _commandQueueManager.GetOrCreateQueueAsync(
                    openVpnServer.ManagementIp, openVpnServer.ManagementPort);
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    _logger.LogInformation(
                        $"⏳ Processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");

                    using var innerScope = _serviceProvider.CreateScope();
                    var innerUnitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var openVpnServerService =
                        innerScope.ServiceProvider.GetRequiredService<IOpenVpnServerService>();
                    var innerOpenVpnServerRepository = innerUnitOfWork.GetRepository<OpenVpnServer>();

                    _logger.LogInformation(
                        $"📌 Saving OpenVPN server status for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                    await openVpnServerService.SaveOpenVpnServerStatusLogAsync(
                        openVpnServer.Id, commandQueue, cancellationToken
                    );

                    _logger.LogInformation(
                        $"📡 Saving connected clients for {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                    await openVpnServerService.SaveConnectedClientsAsync(
                        openVpnServer.Id, commandQueue, cancellationToken
                    );

                    openVpnServer.IsOnline = true;
                    innerOpenVpnServerRepository.Update(openVpnServer);
                    await innerUnitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        $"✅ Finished processing OpenVPN server: {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                }
                catch (Exception ex)
                {
                    using var errorScope = _serviceProvider.CreateScope();
                    var errorUnitOfWork = errorScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var errorRepository = errorUnitOfWork.GetRepository<OpenVpnServer>();

                    openVpnServer.IsOnline = false;
                    errorRepository.Update(openVpnServer);
                    await errorUnitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogError(ex,
                        $"❌ Error processing OpenVPN server {openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
                }
                finally
                {
                    _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
                    await _commandQueueManager.RemoveQueueIfNoSubscribers(openVpnServer.ManagementIp,
                        openVpnServer.ManagementPort);
                    semaphore.Release();
                }
            }));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔥 One or more OpenVPN server processing tasks failed.");
            }
            finally
            {
                _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
                _status.ServiceStatus = ServiceStatus.Idle;
                _logger.LogInformation($"✅ OpenVPN background task completed. Next run at {_nextRunTime}");
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ An error occurred while executing OpenVPN background task. " +
                               $"Message: {ex.Message}";
            _status.ServiceStatus = ServiceStatus.Error;
            _status.ErrorMessage = errorMessage;
            _logger.LogError(ex, errorMessage);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔄 OpenVPN Background Service is starting.");
        
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                if (now < _nextRunTime)
                {
                    var waitTime = (_nextRunTime - now).TotalMilliseconds;
                    _logger.LogInformation($"⏳ Waiting {waitTime / 1000:F0} seconds until next run at {_nextRunTime}");

                    _delayTokenSource = new CancellationTokenSource();
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(waitTime), _delayTokenSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogInformation("🚀 Manual trigger received. Skipping wait.");
                    }
                }

                _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
                await RunOpenVpnTask(cancellationToken);
            }

            _logger.LogInformation("🛑 OpenVPN Background Service is stopping.");
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ An error occurred while running the background service. Message: {ex.Message}";
            _status.ServiceStatus = ServiceStatus.Error;
            _status.ErrorMessage = errorMessage;
            _logger.LogError(ex, errorMessage);
        }
    }
}
