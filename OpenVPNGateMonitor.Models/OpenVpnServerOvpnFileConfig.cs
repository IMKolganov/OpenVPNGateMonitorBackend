using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class OpenVpnServerOvpnFileConfig
{
    public int Id { get; set; }
    [Required]
    public int ServerId { get; set; }
    [Required]
    public string VpnServerIp { get; set; } = string.Empty;
    public int VpnServerPort { get; set; }
    
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public DateTime CreateDate { get; set; }= DateTime.Now;
}