using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Requests;

public class RevokeXrayClientLinkRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Required(ErrorMessage = "issuedXrayClientLinkId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "issuedXrayClientLinkId must be greater than 0.")]
    public int IssuedXrayClientLinkId { get; set; }

    [Required(ErrorMessage = "commonName is required.")]
    public string CommonName { get; set; } = string.Empty;

    public bool IsRevoked { get; set; } = false;
}
