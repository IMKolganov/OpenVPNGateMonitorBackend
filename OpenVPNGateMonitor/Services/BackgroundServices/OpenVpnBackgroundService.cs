using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnBackgroundService : BackgroundService, IOpenVpnBackgroundService
{
    private readonly ILogger<OpenVpnBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _seconds;

    public OpenVpnBackgroundService(ILogger<OpenVpnBackgroundService> logger, 
        IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
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
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("OpenVPN Background Service is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var openVpnServerService = scope.ServiceProvider.GetRequiredService<IOpenVpnServerService>();
                    await openVpnServerService.SaveOpenVpnServerStatusLogAsync(cancellationToken);
                    await openVpnServerService.SaveConnectedClientsAsync(cancellationToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken);
            }

            _logger.LogInformation("OpenVPN Background Service is stopping.");
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"An error occured while running the background service. Error: {ex.Message}");
        }
    }
}