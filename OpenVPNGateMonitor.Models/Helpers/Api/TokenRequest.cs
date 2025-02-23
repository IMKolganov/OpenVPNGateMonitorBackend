namespace OpenVPNGateMonitor.Models.Helpers.Api;

public class TokenRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}