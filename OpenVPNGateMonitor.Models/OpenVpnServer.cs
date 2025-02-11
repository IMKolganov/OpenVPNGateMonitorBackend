namespace OpenVPNGateMonitor.Models;

public class OpenVpnServer
{
    public int Id { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string ManagementIp { get; set; } = string.Empty;
    public int ManagementPort { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = false;
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public DateTime CreateDate { get; set; }= DateTime.Now;
}