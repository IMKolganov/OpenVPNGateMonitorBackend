namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;

public class VpnServerCertificateResponse
{
    public int Id { get; set; }
    public int VpnServerId { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; } = DateTime.MinValue;
    public DateTime? RevokeDate { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string UnknownField { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
}