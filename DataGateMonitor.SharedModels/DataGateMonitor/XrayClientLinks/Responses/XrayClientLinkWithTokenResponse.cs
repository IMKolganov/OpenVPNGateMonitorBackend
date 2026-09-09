using DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses;

public class XrayClientLinkWithTokenResponse
{
    public IssuedXrayClientLinkDto IssuedXrayClientLink { get; set; } = new();
    public IssuedXrayClientLinkTokenDto IssuedXrayClientLinkToken { get; set; } = new();
}
