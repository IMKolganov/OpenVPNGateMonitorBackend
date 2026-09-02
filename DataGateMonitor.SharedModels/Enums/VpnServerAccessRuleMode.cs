namespace DataGateMonitor.SharedModels.Enums;

/// <summary>
/// Per-user override of the quota-plan server allowlist. <see cref="Deny"/> wins over
/// <see cref="Allow"/> and over the plan, including for users without an active plan.
/// </summary>
public enum VpnServerAccessRuleMode
{
    Allow = 1,
    Deny = 2,
}
