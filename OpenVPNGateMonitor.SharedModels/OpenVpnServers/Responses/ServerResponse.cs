namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class ServerResponse
{
    public int ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string ManagementIp { get; set; } = string.Empty;
    public int ManagementPort { get; set; }
    public bool IsOnline { get; set; }
}