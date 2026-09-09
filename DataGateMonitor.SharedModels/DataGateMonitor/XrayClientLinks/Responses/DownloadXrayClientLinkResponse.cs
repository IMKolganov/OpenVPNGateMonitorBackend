using DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses;

public class DownloadXrayClientLinkResponse
{
    public IssuedXrayClientLinkDto IssuedXrayClientLink { get; set; } = new();
    public long FileSizeBytes { get; set; }
    public byte[]? Content { get; set; }
}
