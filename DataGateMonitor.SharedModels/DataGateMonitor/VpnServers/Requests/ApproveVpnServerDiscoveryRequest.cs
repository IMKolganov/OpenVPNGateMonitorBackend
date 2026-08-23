namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;

public class ApproveVpnServerDiscoveryRequest
{
    /// <summary>Optional override for the server display name. Falls back to discovery SuggestedName.</summary>
    public string? ServerName { get; set; }

    public bool IsDefault { get; set; }

    public bool IsEnableWss { get; set; }

    public bool IsPiHoleEnabled { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public List<int> QuotaPlanIds { get; set; } = new();

    public List<int> TagIds { get; set; } = new();

    /// <summary>When true, background polling is disabled for this server after creation.</summary>
    public bool IsDisabled { get; set; }
}
