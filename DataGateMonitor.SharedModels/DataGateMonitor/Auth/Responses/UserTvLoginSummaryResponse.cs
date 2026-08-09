namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>Per-user summary of TV device-linking usage (admin).</summary>
public sealed class UserTvLoginSummaryResponse
{
    /// <summary>True if the user ever approved a TV login session (Approved or Consumed).</summary>
    public bool HasUsedTvLogin { get; set; }

    public int ApprovedOrConsumedCount { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public string? LastDeviceName { get; set; }

    public string? LastClient { get; set; }
}
