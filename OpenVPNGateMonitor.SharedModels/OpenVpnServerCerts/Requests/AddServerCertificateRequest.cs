using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;

public class AddServerCertificateRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Required(ErrorMessage = "cnName is required.")]
    public string CnName { get; set; } = string.Empty;
}