using DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses;

public class XrayClientLinkResponse
{
    public IssuedXrayClientLinkDto IssuedXrayClientLink { get; set; } = new();
}
