using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnClientService
{
    Task<List<OpenVpnClient>> GetClientsAsync(string managementIp, int managementPort, CancellationToken cancellationToken);
}