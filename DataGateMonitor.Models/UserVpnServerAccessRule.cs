using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Models;

/// <summary>
/// Per-user override of the quota-plan server allowlist. At most one rule per (user, server);
/// a <see cref="VpnServerAccessRuleMode.Deny"/> rule wins over the plan and over an Allow rule.
/// </summary>
public class UserVpnServerAccessRule : BaseEntity<int>
{
    public int UserId { get; set; }

    public int VpnServerId { get; set; }

    public VpnServerAccessRuleMode Mode { get; set; } = VpnServerAccessRuleMode.Allow;
}
