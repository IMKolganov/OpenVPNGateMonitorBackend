using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class ServerInfo
{
    public string Status { get; set; } = string.Empty;
    public OpenVpnState? OpenVpnState { get; set; }
    public OpenVpnSummaryStats? OpenVpnSummaryStats { get; set; }
    public string Version { get; set; } = string.Empty;
}