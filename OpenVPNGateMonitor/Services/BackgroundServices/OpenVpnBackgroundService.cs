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
                    _logger.LogInformation($"State: {state.RemoteIp} {state.Success} {state.Connected} {state.LocalIp} ");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN state.");
                }
                var openVpnSummaryStatService = scope.ServiceProvider.GetRequiredService<IOpenVpnSummaryStatService>();
                try
                {
                    var summaryStats = await openVpnSummaryStatService.GetSummaryStatsAsync(stoppingToken);
                    _logger.LogInformation($"Summary stats: {summaryStats.BytesIn} {summaryStats.BytesOut} {summaryStats.ClientsCount} ");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN summary stats.");
                }
                var openVpnClientService = scope.ServiceProvider.GetRequiredService<IOpenVpnClientService>();
                try
                {
                    var clients= await openVpnClientService.GetClientsAsync(stoppingToken);
                    foreach (var client in clients)
                    {
                        _logger.LogInformation($"Clients: {client.CommonName} {client.RemoteIp} {client.LocalIp} {client.BytesReceived} {client.BytesSent} {client.Country}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while parsing OpenVPN clients.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(Seconds), stoppingToken);
        }

        _logger.LogInformation("OpenVPN Background Service is stopping.");
    }
}