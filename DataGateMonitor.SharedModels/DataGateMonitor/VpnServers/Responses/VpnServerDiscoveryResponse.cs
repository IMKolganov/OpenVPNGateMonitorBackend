using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

public class VpnServerDiscoveryResponse
{
    public VpnServerDiscoveryDto Discovery { get; set; } = new();

    public int? VpnServerId { get; set; }
}
