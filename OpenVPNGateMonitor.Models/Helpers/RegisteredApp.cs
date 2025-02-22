using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models.Helpers;

public class RegisteredApp
{
    [Key]
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string Name { get; set; }

    public string ClientSecret { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}