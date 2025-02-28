namespace OpenVPNGateMonitor.Models;

public class OpenVpnServer : BaseEntity<int>
{
    public string ServerName { get; set; } = string.Empty;
    public string ManagementIp { get; set; } = string.Empty;
    public int ManagementPort { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = false;
}