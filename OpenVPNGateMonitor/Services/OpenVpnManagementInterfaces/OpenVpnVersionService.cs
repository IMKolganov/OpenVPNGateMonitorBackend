using MaxMind.GeoIP2;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnVersionService
{
    private readonly ILogger<OpenVpnVersionService> _logger;
    private readonly IOpenVpnManagementService _openVpnManagementService;
    
    public OpenVpnVersionService(ILogger<OpenVpnVersionService> logger, 
        IOpenVpnManagementService openVpnManagementService)
    {
        _logger = logger;
        _openVpnManagementService = openVpnManagementService;
    }
    
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        return await _openVpnManagementService.SendCommandAsync("version", cancellationToken);
    }

}