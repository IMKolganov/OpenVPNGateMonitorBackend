namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class RevokeCertificateRequest
{
    public int VpnServerId { get; set; }
    public string CommonName { get; set; } = string.Empty;
}