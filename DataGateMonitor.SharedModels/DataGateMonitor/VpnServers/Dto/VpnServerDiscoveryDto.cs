using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

public class VpnServerDiscoveryDto
{
    public int Id { get; set; }

    public VpnServerType ServerType { get; set; }

    public string ApiUrl { get; set; } = string.Empty;

    public string? SuggestedName { get; set; }

    public string? PublicIp { get; set; }

    public string? Version { get; set; }

    public bool IsEnableWss { get; set; }

    public VpnServerDiscoveryStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    public int? ResolvedVpnServerId { get; set; }
}
