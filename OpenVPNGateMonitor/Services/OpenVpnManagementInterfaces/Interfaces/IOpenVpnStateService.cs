using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnStateService
{
    Task<OpenVpnState> GetStateAsync(CancellationToken cancellationToken);
}