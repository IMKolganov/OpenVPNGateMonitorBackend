namespace DataGateMonitor.SharedModels.DataGateMonitor.XrayClientLinks.Responses.Dto;

public class IssuedXrayClientLinkWithTokenDto
{
    public IssuedXrayClientLinkDto IssuedXrayClientLink { get; set; } = new();
    public IssuedXrayClientLinkTokenDto IssuedXrayClientLinkToken { get; set; } = new();
}
