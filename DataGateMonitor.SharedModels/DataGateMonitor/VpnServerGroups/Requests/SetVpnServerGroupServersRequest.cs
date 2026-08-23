using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;

public class SetVpnServerGroupServersRequest
{
    /// <summary>Ordered membership for the target group (or ungrouped when used with the ungrouped endpoint).</summary>
    [Required]
    public List<int> VpnServerIds { get; set; } = [];
}
