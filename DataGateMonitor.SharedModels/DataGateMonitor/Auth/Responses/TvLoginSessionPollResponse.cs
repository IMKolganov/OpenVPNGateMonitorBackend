namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>
/// TV poll payload. While pending/denied/expired/consumed, only <see cref="Status"/> and <see cref="ExpiresAt"/> are set.
/// When <see cref="Status"/> is <c>approved</c>, login token fields match <see cref="LoginResponse"/> (one-time delivery).
/// </summary>
public sealed class TvLoginSessionPollResponse
{
    /// <summary>pending | viewed | approved | denied | expired | consumed</summary>
    public string Status { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public int UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Token { get; set; }
    public DateTimeOffset Expiration { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshExpiration { get; set; }
    public bool RequiresTotp { get; set; }
    public string? LoginChallengeId { get; set; }
    public bool RequiresTotpSetup { get; set; }
}
