namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

public class OvpnFileResponse
{
    public int Id { get; set; }
    public int VpnServerId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? CertId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public string IssuedTo { get; set; } = string.Empty;
    public string PemFilePath { get; set; } = null!;
    public string CertFilePath { get; set; } = null!;
    public string KeyFilePath { get; set; } = null!;
    public string ReqFilePath { get; set; } = null!;
    public bool IsRevoked { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
    public DateTime LastUpdate { get; set; }
}