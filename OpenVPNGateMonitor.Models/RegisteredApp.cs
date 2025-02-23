using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class RegisteredApp
{
    public int Id { get; set; }
    [Key]
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string Name { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsRevoked { get; set; } = false;
    public DateTime LastUpdate { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}