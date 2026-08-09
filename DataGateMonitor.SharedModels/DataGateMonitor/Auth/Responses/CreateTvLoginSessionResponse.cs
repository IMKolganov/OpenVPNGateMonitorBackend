namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>Payload returned when a TV creates a short-lived login session (QR + user code).</summary>
public sealed class CreateTvLoginSessionResponse
{
    public Guid SessionId { get; set; }

    /// <summary>6-digit numeric code shown on the TV (e.g. 482913). Also embedded in <see cref="QrPayload"/>.</summary>
    public string UserCode { get; set; } = null!;

    /// <summary>Landing page for phone users (without code query).</summary>
    public string VerificationUrl { get; set; } = null!;

    /// <summary>Exact string to encode in the QR code (verification URL with code query).</summary>
    public string QrPayload { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Suggested poll interval for GET /api/auth/tv/session/{sessionId} when SignalR is unavailable.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Anonymous SignalR hub path; call WatchSession(sessionId) and listen for TvLoginSessionStatusChanged.</summary>
    public string SignalRHubPath { get; set; } = "/api/hubs/tv-login";
}
