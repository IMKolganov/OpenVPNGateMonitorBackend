using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

public class AnnounceVpnServerResponse
{
    public AnnounceVpnServerResultStatus Status { get; set; }

    public int? DiscoveryId { get; set; }

    public int? ExistingVpnServerId { get; set; }
}
