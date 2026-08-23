using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Responses;

public class VpnServerGroupResponse
{
    public VpnServerGroupDto Group { get; set; } = new();
}
