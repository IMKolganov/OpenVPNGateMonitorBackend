using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IOpenVpnSummaryStatService
{
    Task<OpenVpnSummaryStats> GetSummaryStatsAsync(ICommandQueue commandQueue, CancellationToken cancellationToken);
}