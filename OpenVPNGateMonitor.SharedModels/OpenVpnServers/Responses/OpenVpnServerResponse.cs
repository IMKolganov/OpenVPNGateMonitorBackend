namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class OpenVpnServerResponse
{
    public int Id { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string ManagementIp { get; set; } = string.Empty;
    public int ManagementPort { get; set; }
    public string? Login { get; set; } = string.Empty;
    public string? Password { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = false;
    public DateTime CreateDate { get; set; }
    public DateTime LastUpdate { get; set; }
}