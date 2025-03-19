namespace OpenVPNGateMonitor.SharedModels.Applications.Responses;

public class ApplicationDto
{
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; } = DateTime.MinValue;
    public DateTime LastUpdate { get; set; } = DateTime.MinValue;
}