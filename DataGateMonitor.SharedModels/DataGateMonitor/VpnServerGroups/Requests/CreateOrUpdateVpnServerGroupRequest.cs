using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;

public class CreateOrUpdateVpnServerGroupRequest
{
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;
}
