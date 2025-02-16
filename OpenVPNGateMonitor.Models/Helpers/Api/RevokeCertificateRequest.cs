namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class RevokeCertificateRequest
{
    public int VpnServerId { get; set; }
    public string CnName { get; set; }
}