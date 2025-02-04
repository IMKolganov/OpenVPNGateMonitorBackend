using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.Models.Helpers;

public class CertificateCaInfo
{
    public CertificateStatus Status { get; set; } = CertificateStatus.Unknown;
    public DateTime? ExpiryDate { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string UnknownField { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
}