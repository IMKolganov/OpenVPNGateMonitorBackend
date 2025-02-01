namespace OpenVPNGateMonitor.Models.Helpers;

public class OpenVpnServerFullInfo
{
    public OpenVpnServerInfo OpenVpnServerInfo { get; set; } = new OpenVpnServerInfo();
    public List<OpenVpnUserStatistic> OpenVpnUserStatistics { get; set; } = new List<OpenVpnUserStatistic>();
}