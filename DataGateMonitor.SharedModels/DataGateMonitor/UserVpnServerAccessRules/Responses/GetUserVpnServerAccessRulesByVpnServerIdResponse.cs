using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Responses;

public class GetUserVpnServerAccessRulesByVpnServerIdResponse
{
    public List<UserVpnServerAccessRuleDto> Items { get; set; } = new();
}
