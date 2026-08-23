using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;

public class ReorderVpnServerGroupsRequest
{
    [Required]
    public List<VpnServerGroupOrderItem> Items { get; set; } = [];
}

public class VpnServerGroupOrderItem
{
    [Required]
    public int GroupId { get; set; }

    [Required]
    public int SortOrder { get; set; }
}
