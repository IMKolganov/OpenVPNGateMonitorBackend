namespace OpenVPNGateMonitor.Models.Helpers.Services;

public class OpenVpnServerClientList
{
    public List<OpenVpnServerClient> OpenVpnServerClients { get; set; } = new List<OpenVpnServerClient>();
    public int TotalCount { get; set; }
}