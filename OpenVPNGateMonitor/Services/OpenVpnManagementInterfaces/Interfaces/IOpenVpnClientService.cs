using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnClientService
{
    Task<List<OpenVpnClient>> GetClientsAsync(ICommandQueue commandQueue, 
        CancellationToken cancellationToken);
}