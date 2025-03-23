namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class OpenVpnServerWithStatusResponse
{
    public OpenVpnServerResponse OpenVpnServerResponses { get; set; } = new();
    public OpenVpnServerStatusLogResponse? OpenVpnServerStatusLogResponse { get; set; }
    public long TotalBytesIn { get; set; }
    public long TotalBytesOut { get; set; }
}