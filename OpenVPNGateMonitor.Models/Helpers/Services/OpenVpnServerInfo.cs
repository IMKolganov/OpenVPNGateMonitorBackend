namespace OpenVPNGateMonitor.Models.Helpers.Services;

public class OpenVpnServerInfo
{
    public required OpenVpnServer OpenVpnServer { get; set; }
    public OpenVpnServerStatusLog? OpenVpnServerStatusLog { get; set; }
    public long TotalBytesIn { get; set; }
    public long TotalBytesOut { get; set; }
}