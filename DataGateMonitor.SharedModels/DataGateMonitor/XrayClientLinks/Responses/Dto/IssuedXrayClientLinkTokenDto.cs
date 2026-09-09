namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

public class IssuedXrayClientLinkTokenDto
{
    public int Id { get; set; }
    public int IssuedXrayClientLinkId { get; set; }
    public string Token { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public string? Purpose { get; set; }
    public DateTimeOffset CreateDate { get; set; }
    public DateTimeOffset LastUpdate { get; set; }
}
