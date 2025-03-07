namespace OpenVPNGateMonitor.Models.Helpers.Auth;

public class SetSecretRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}