using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Responses;

public class VpnServerGroupsResponse
{
    public List<VpnServerGroupDto> Groups { get; set; } = [];
}
