using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;

public class RevokeOvpnFileRequest
{
    [Required(ErrorMessage = "serverId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "serverId must be greater than 0.")]
    public int ServerId { get; set; }

    [Required(ErrorMessage = "commonName is required.")]
    public string CommonName { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;
}