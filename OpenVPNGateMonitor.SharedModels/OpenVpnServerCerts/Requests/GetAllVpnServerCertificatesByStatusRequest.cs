using System.ComponentModel.DataAnnotations;
using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;

public class GetAllVpnServerCertificatesByStatusRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [Required(ErrorMessage = "certificateStatus is required.")]
    public CertificateStatus CertificateStatus { get; set; }
}