namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class OpenVpnServerInfoResponse
{
    public required OpenVpnServer OpenVpnServer { get; set; }
    public OpenVpnServerStatusLog? OpenVpnServerStatusLog { get; set; }
}