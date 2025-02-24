namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class OpenVpnServerClientsResponse
{
    public List<OpenVpnServerClient> OpenVpnServerClients { get; set; } = new List<OpenVpnServerClient>();
    public int TotalCount { get; set; }
}