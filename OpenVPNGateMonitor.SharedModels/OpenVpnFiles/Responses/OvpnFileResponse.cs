namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class OvpnFileResponse
{
    public int ServerId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public string IssuedTo { get; set; } = string.Empty;
    public bool IsRevoked { get; set; } = false;
    public string Message { get; set; } = string.Empty;
}