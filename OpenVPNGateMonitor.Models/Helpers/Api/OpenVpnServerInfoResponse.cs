namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class OpenVpnServerInfoResponse
{
    public required OpenVpnServer OpenVpnServer { get; set; }
    public OpenVpnServerStatusLog? OpenVpnServerStatusLog { get; set; }
    public long TotalBytesIn { get; set; }
    public long TotalBytesOut { get; set; }
}