namespace DataGateMonitor.Models;

public class VpnServerPiHoleConfig : BaseEntity<int>
{
    public int VpnServerId { get; set; }

    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    public string AppPassword { get; set; } = string.Empty;

    public int PollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 200;

    public int LookbackSeconds { get; set; } = 120;

    /// <summary>Only store queries from clients whose IP starts with this prefix (e.g. 10.51.30.). Empty = no filter.</summary>
    public string ClientSubnetPrefix { get; set; } = string.Empty;

    /// <summary>When dashboard ApplyRuntime last pushed Pi-hole config to the VPN node.</summary>
    public DateTimeOffset? LastRuntimeAppliedAtUtc { get; set; }
}
