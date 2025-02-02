using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnManagementService
{
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
    Task<OpenVpnState> GetStateAsync(CancellationToken cancellationToken);
    Task<OpenVpnStats> GetStatsAsync(CancellationToken cancellationToken);
    Task<List<OpenVpnClient>> GetClientsAsync(CancellationToken cancellationToken);
}