namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

public class IssuedXrayClientLinkDto
{
    public int Id { get; set; }
    public int VpnServerId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string CommonName { get; set; } = null!;
    public string? CertId { get; set; } = string.Empty;
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public DateTimeOffset IssuedAt { get; set; }
    public string IssuedTo { get; set; } = null!;
    public string PemFilePath { get; set; } = null!;
    public string CertFilePath { get; set; } = null!;
    public string KeyFilePath { get; set; } = null!;
    public string ReqFilePath { get; set; } = null!;
    public bool IsRevoked { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreateDate { get; set; }
    public DateTimeOffset LastUpdate { get; set; }
}
