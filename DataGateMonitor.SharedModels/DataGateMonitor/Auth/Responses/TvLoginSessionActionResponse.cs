namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

/// <summary>Result of approve/deny on a TV login session.</summary>
public sealed class TvLoginSessionActionResponse
{
    /// <summary>approved | denied</summary>
    public string Status { get; set; } = null!;
}
