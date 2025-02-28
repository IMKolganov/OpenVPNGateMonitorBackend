using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class OpenVpnServerStatusLog : BaseEntity<int>
{
    [Required]
    public int VpnServerId { get; set; }
    [Required]
    public Guid SessionId { get; set; }
    public DateTime UpSince { get; set; }
    [MaxLength(255)]
    public string ServerLocalIp { get; set; } = string.Empty;
    [MaxLength(255)]
    public string ServerRemoteIp { get; set; } = string.Empty;
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }
    [MaxLength(255)]
    public string Version { get; set; } = string.Empty;
}