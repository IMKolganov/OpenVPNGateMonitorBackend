namespace DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

/// <summary>
/// Active quota plan and allowed VPN servers for the authenticated user (API v3 list endpoints).
/// </summary>
public class UserQuotaPlanContextDto
{
    /// <summary>Admin/App: no quota restriction on detail endpoints.</summary>
    public bool IsPrivileged { get; set; }

    /// <summary>Active assignment id, when present.</summary>
    public int? UserQuotaPlanId { get; set; }

    public int? QuotaPlanId { get; set; }

    public string? QuotaPlanName { get; set; }

    /// <summary>
    /// Effective VPN server ids the user may connect to: the active plan allowlist plus
    /// <see cref="PersonalAllowedVpnServerIds"/>, minus <see cref="PersonalDeniedVpnServerIds"/>.
    /// Empty when <see cref="IsPrivileged"/> or no plan, which both mean "not restricted to a list".
    /// </summary>
    public List<int> AllowedVpnServerIds { get; set; } = [];

    /// <summary>VPN server ids granted to this user personally, on top of the quota plan.</summary>
    public List<int> PersonalAllowedVpnServerIds { get; set; } = [];

    /// <summary>VPN server ids blocked for this user personally. Wins over the plan and over personal grants.</summary>
    public List<int> PersonalDeniedVpnServerIds { get; set; } = [];
}
