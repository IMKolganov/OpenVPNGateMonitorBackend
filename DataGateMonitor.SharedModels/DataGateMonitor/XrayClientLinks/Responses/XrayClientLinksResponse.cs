using DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses;

public class XrayClientLinksResponse
{
    public List<IssuedXrayClientLinkDto> IssuedXrayClientLinks { get; set; } = new();
}
