using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private readonly ILogger<OpenVpnBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private const int Seconds = 3;

    public OpenVpnBackgroundService(ILogger<OpenVpnBackgroundService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpenVPN Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var vpnManagementService = scope.ServiceProvider.GetRequiredService<IVpnManagementService>();
                try
                {
                    _logger.LogInformation("Parsing OpenVPN status file at {Time}", DateTimeOffset.Now);
                    var version = await vpnManagementService.GetVersionAsync();
                    var state = await vpnManagementService.GetStateAsync();
                    var stats = await vpnManagementService.GetStatsAsync();
                    var clients= await vpnManagementService.GetClientsAsync();

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN status file.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(Seconds), stoppingToken);
        }

        _logger.LogInformation("OpenVPN Background Service is stopping.");
    }
}