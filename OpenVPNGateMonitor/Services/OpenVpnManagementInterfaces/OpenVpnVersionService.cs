using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnVersionService
{
    public OpenVpnVersionService(IOpenVpnManagementService)
    {
        
    }
    
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        return await SendCommandAsync("version", cancellationToken);
    }

}