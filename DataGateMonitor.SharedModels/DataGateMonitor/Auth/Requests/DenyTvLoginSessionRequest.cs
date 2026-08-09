namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;

/// <summary>Phone/web denies a pending TV login session (provide sessionId and/or userCode).</summary>
public sealed class DenyTvLoginSessionRequest
{
    public Guid? SessionId { get; set; }

    /// <summary>Human-readable code shown on the TV (hyphens/spaces optional).</summary>
    public string? UserCode { get; set; }
}
