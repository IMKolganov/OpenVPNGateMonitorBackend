using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;

public class AddOvpnFileRequest
{
    [Required(ErrorMessage = "externalId is required.")]
    public string ExternalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "commonName is required.")]
    public string CommonName { get; set; } = string.Empty;

    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Required(ErrorMessage = "issuedTo is required.")]
    public string IssuedTo { get; set; } = "openVpnClient";
}