using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private readonly ILogger<OpenVpnBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _seconds;
    private DateTime _nextRunTime;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private BackgroundServiceStatus _status = BackgroundServiceStatus.Idle;

    public OpenVpnBackgroundService(ILogger<OpenVpnBackgroundService> logger, 
        IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _logger.LogInformation($"Starting background service{Guid.NewGuid()}");
        var openVpnSection = configuration.GetSection("OpenVpn");
        if (!openVpnSection.Exists())
        {
            throw new InvalidOperationException("OpenVpn section is missing in the configuration.");
        }

        _seconds = openVpnSection.Get<OpenVpnSettings>()!.UpdateSecond;
        if (_seconds <= 0)
        {
            throw new InvalidOperationException("Failed to load OpenVpnSettings UpdateSecond from configuration.");
        }

        _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
    }

    public DateTime GetNextRunTime()
    {
        return _nextRunTime;
    }

    public BackgroundServiceStatus GetStatus()
    {
        return _status;
    }

    public async Task RunNow(CancellationToken cancellationToken)
    {
        if (!_executionLock.Wait(0)) return; // Prevent multiple executions

        try
        {
            await ExecuteTask(cancellationToken);
        }
        finally
        {
            _executionLock.Release();
        }
    }

    private new async Task ExecuteTask(CancellationToken cancellationToken)
    {
        if (_status == BackgroundServiceStatus.Running) return;

        _status = BackgroundServiceStatus.Running;
        try
        {
            _logger.LogInformation("Executing OpenVPN background task...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var openVpnServerService = scope.ServiceProvider.GetRequiredService<IOpenVpnServerService>();
                await openVpnServerService.SaveOpenVpnServerStatusLogAsync(cancellationToken);
                await openVpnServerService.SaveConnectedClientsAsync(cancellationToken);
            }

            _nextRunTime = DateTime.UtcNow.AddSeconds(_seconds);
            _status = BackgroundServiceStatus.Idle;
        }
        catch (Exception ex)
        {
            _status = BackgroundServiceStatus.Error;
            _logger.LogError(ex, $"An error occurred while executing OpenVPN background task.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("OpenVPN Background Service is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                await ExecuteTask(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken);
            }

            _logger.LogInformation("OpenVPN Background Service is stopping.");
        }
        catch (Exception ex)
        {
            _status = BackgroundServiceStatus.Error;
            _logger.LogError(ex, $"An error occurred while running the background service.");
        }
    }
}
