using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

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
                var openVpnStateService = scope.ServiceProvider.GetRequiredService<IOpenVpnStateService>();
                try
                {
                    var state = await openVpnStateService.GetStateAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN status file.");
                }
                var openVpnSummaryStatService = scope.ServiceProvider.GetRequiredService<IOpenVpnSummaryStatService>();
                try
                {
                    var stats = await openVpnSummaryStatService.GetSummaryStatsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN status file.");
                }
                var openVpnClientService = scope.ServiceProvider.GetRequiredService<IOpenVpnClientService>();
                try
                {
                    var clients= await openVpnClientService.GetClientsAsync(stoppingToken);
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