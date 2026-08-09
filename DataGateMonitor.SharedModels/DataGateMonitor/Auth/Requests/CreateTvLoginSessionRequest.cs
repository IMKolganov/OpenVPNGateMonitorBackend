namespace DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;

/// <summary>Optional metadata when a TV (or similar device) starts a device-linking login session.</summary>
public sealed class CreateTvLoginSessionRequest
{
    /// <summary>Human-readable device label, e.g. "Living Room TV".</summary>
    public string? DeviceName { get; set; }

    /// <summary>Client identifier, e.g. "android-tv".</summary>
    public string? Client { get; set; }
}
