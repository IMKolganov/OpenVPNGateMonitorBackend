namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>Authenticated phone/web preview of a pending TV login session by user code.</summary>
public sealed class TvLoginSessionPreviewResponse
{
    public Guid SessionId { get; set; }

    /// <summary>Formatted user code (e.g. ABCD-1234).</summary>
    public string UserCode { get; set; } = null!;

    public string? DeviceName { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Expected value for approve UI: pending</summary>
    public string Status { get; set; } = null!;
}
