namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>SignalR payload for <c>TvLoginSessionStatusChanged</c> on /api/hubs/tv-login.</summary>
public sealed class TvLoginSessionStatusEvent
{
    public Guid SessionId { get; set; }

    /// <summary>pending | viewed | approved | denied | expired | consumed</summary>
    public string Status { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
}
