using System.ComponentModel.DataAnnotations;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;

public class CreateOrUpdateUserVpnServerAccessRuleRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "Id is required for update.")]
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "UserId must be greater than 0.")]
    public int UserId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "VpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    [EnumDataType(typeof(VpnServerAccessRuleMode), ErrorMessage = "Mode must be Allow or Deny.")]
    public VpnServerAccessRuleMode Mode { get; set; } = VpnServerAccessRuleMode.Allow;
}
