namespace DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;

/// <summary>
/// Per-user grants and blocks layered on top of the quota-plan allowlist.
/// A block wins over both a personal grant and the plan, including for users without an active plan.
/// </summary>
public sealed record UserVpnServerAccessOverrides(
    HashSet<int> AllowedVpnServerIds,
    HashSet<int> DeniedVpnServerIds)
{
    public static UserVpnServerAccessOverrides None => new([], []);

    public bool IsEmpty => AllowedVpnServerIds.Count == 0 && DeniedVpnServerIds.Count == 0;

    public bool Allows(int vpnServerId, bool allowedByQuotaPlan)
        => !DeniedVpnServerIds.Contains(vpnServerId)
           && (allowedByQuotaPlan || AllowedVpnServerIds.Contains(vpnServerId));

    /// <summary>
    /// Stable key fragment so that responses cached per quota plan are not shared
    /// between users whose personal rules differ.
    /// </summary>
    public string CacheScopeSuffix => IsEmpty
        ? string.Empty
        : $":allow={string.Join(',', AllowedVpnServerIds.Order())}:deny={string.Join(',', DeniedVpnServerIds.Order())}";
}
