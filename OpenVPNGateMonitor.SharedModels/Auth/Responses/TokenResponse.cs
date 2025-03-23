namespace OpenVPNGateMonitor.SharedModels.Auth.Responses;

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; } = DateTime.MinValue;
}