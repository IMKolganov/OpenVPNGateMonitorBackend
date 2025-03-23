namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;

public class RevokeCertificateResponse
{
    public bool IsRevoked { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = string.Empty;
}