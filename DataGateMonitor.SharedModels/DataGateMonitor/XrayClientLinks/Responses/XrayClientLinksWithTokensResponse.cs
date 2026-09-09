using DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses;

public class XrayClientLinksWithTokensResponse
{
    public List<IssuedXrayClientLinkDto> IssuedXrayClientLinks { get; set; } = new();
    public List<IssuedXrayClientLinkTokenDto> IssuedXrayClientLinkTokens { get; set; } = new();
}
