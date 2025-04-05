using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class IssuedOvpnFile : BaseEntity<int>
{
    [Required]
    public int VpnServerId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    [Required]
    public string CommonName { get; set; } = null!;
    public string? CertId { get; set; } = string.Empty;
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public string IssuedTo { get; set; } = null!;
    [Required]
    public string PemFilePath { get; set; } = null!;
    [Required]
    public string CertFilePath { get; set; } = null!;
    [Required]
    public string KeyFilePath { get; set; } = null!;
    [Required]
    public string ReqFilePath { get; set; } = null!;
    [Required]
    public bool IsRevoked { get; set; } = false;
    public string Message { get; set; } = string.Empty;
}
