using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnManagementService
{
    Task<string> SendCommandAsync(string managementIp, int managementPort,
        string command, CancellationToken cancellationToken);
}