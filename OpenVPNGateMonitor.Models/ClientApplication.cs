using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class ClientApplication : BaseEntity<int>
{
    [Key]
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string Name { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsRevoked { get; set; } = false;
    public bool IsSystem { get; set; } = false;
}