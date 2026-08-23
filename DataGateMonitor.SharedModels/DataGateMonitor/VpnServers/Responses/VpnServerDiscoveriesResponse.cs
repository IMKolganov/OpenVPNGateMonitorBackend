using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

public class VpnServerDiscoveriesResponse
{
    public List<VpnServerDiscoveryDto> Discoveries { get; set; } = new();
}
