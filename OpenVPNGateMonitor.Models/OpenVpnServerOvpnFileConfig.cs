using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class OpenVpnServerOvpnFileConfig : BaseEntity<int>
{
    [Required]
    public int ServerId { get; set; }
    [Required]
    public string VpnServerIp { get; set; } = string.Empty;
    public int VpnServerPort { get; set; }
    public string ConfigTemplate { get; set; } = string.Empty;
}