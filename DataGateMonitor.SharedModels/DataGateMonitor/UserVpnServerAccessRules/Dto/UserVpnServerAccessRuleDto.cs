using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Dto;

public class UserVpnServerAccessRuleDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VpnServerId { get; set; }
    public VpnServerAccessRuleMode Mode { get; set; }
}
