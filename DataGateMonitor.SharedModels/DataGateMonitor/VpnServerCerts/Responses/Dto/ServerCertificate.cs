using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerCerts.Responses.Dto;

public class MonitorServerCertificate
{
    public int VpnServerId { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public CertificateStatus Status { get; set; } = CertificateStatus.Unknown;
    public string SerialNumber { get; set; } = string.Empty;
    public string UnknownField { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public DateTimeOffset ExpiryDate { get; set; }
    public DateTimeOffset? RevokeDate { get; set; }

    /// <summary>
    /// Xray DNS identity source IP (Pi-hole ClientIp matcher). Null for OpenVPN or when identity is off.
    /// </summary>
    public string? IdentityIp { get; set; }
}
