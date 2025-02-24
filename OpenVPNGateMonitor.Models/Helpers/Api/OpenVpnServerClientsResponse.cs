namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class OpenVpnServerClientsResponse
{
    public List<OpenVpnServerClient> OpenVpnServerClients { get; set; }
    public int TotalCount { get; set; }
}