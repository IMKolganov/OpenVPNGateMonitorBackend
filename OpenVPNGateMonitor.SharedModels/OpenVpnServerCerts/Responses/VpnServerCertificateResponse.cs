namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;

public class VpnServerCertificateResponse
{
    public int Id { get; set; }
    public int VpnServerId { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string CertificateData { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public bool IsRevoked { get; set; }
}