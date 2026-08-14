namespace DataGateMonitor.Models.Auth;

/// <summary>
/// Development-only token payload. For <c>OpenVpn</c> also returns material needed to understand / mint calls.
/// </summary>
public sealed class DevTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset Expiration { get; set; }
    public string Role { get; set; } = string.Empty;

    /// <summary>JWT issuer (OpenVpn microservice tokens).</summary>
    public string? Issuer { get; set; }

    /// <summary>JWT audience expected by OpenVPN manager (<c>DataGateOpenVpnManager</c>).</summary>
    public string? Audience { get; set; }

    /// <summary>Required claim <c>purpose</c> on OpenVPN manager tokens.</summary>
    public string? Purpose { get; set; }

    /// <summary>Required role claim value on OpenVPN manager tokens (<c>backend</c>).</summary>
    public string? MicroserviceRole { get; set; }

    /// <summary>RSA public key PEM used by OpenVPN manager to validate microservice JWTs.</summary>
    public string? PublicKeyPem { get; set; }
}
