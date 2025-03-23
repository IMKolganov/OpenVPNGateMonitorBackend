namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class ConnectedClientsResponse
{
    public int TotalCount { get; set; }
    public List<VpnClientInfoResponse> Clients { get; set; } = new();
}