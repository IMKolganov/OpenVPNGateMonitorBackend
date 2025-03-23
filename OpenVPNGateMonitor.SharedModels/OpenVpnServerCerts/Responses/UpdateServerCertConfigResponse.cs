namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;

public class UpdateServerCertConfigResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } =  string.Empty;
    public int VpnServerId { get; set; }
}