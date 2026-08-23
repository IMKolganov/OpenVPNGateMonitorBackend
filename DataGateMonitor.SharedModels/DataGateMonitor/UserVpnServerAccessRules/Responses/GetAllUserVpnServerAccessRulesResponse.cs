using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Dto;

namespace DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Responses;

public class GetAllUserVpnServerAccessRulesResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<UserVpnServerAccessRuleDto> Items { get; set; } = new();
}
