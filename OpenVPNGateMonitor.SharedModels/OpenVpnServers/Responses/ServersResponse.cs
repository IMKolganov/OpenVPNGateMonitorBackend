namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class ServersResponse
{
    public List<ServerInfoResponse> Servers { get; set; } = new();
}