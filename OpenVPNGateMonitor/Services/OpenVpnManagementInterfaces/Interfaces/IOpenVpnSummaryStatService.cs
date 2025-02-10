using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnSummaryStatService
{
    Task<OpenVpnSummaryStats> GetSummaryStatsAsync(string managementIp, int managementPort, 
        CancellationToken cancellationToken);
}