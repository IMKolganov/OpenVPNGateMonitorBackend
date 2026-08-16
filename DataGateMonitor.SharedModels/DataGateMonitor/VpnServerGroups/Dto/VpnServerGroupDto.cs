namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Dto;

public class VpnServerGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<int> ServerIds { get; set; } = [];
}
