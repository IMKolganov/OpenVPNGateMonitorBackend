using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.Models;

public class VpnServerGroup : BaseEntity<int>
{
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
