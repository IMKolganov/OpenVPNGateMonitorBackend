namespace OpenVPNGateMonitor.Models.Helpers;

public class OpenVpnServerFullInfo
{
    public OpenVpnServerInfo OpenVpnServerInfo { get; set; } = new OpenVpnServerInfo();
    public List<OpenVpnUserSessionStatistic> OpenVpnUserStatistics { get; set; } = new List<OpenVpnUserSessionStatistic>();
}