using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.VpnAccess;

public interface IVpnServerQuotaPlanAccessGuard
{
    /// <summary>
    /// Ensures the dashboard user linked to <paramref name="externalId"/> may use <paramref name="vpnServerId"/>.
    /// Dashboard admins bypass every check. A personal access rule wins over the quota-plan allowlist;
    /// otherwise users without a link or without an active plan are allowed.
    /// </summary>
    Task EnsureTargetUserMayUseServerAsync(string? externalId, int vpnServerId, CancellationToken ct);
}

public sealed class VpnServerQuotaPlanAccessGuard(
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService,
    IUserVpnServerAccessRuleQueryService userVpnServerAccessRuleQueryService,
    IUserQueryService userQueryService) : IVpnServerQuotaPlanAccessGuard
{
    public async Task EnsureTargetUserMayUseServerAsync(string? externalId, int vpnServerId, CancellationToken ct)
    {
        var trimmed = externalId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        var link = await userIdentityLinkQueryService.GetByExternalId(trimmed, ct);
        if (link is not { UserId: > 0 })
            return;

        var user = await userQueryService.GetById(link.UserId, ct);
        if (user is { IsAdmin: true })
            return;

        var rule = await userVpnServerAccessRuleQueryService.GetByUserIdAndServerId(link.UserId, vpnServerId, ct);
        if (rule is not null)
        {
            if (rule.Mode == VpnServerAccessRuleMode.Allow)
                return;

            throw new InvalidOperationException(VpnServerAccessErrorKeys.NotAllowedByQuotaPlan);
        }

        var activePlan = await userQuotaPlanQueryService.GetActiveByUserId(link.UserId, ct);
        if (activePlan is null)
            return;

        var allowed = await quotaPlanAllowedServerQueryService.GetByQuotaPlanIdAndServerId(
            activePlan.QuotaPlanId,
            vpnServerId,
            ct);
        if (allowed is not null)
            return;

        throw new InvalidOperationException(VpnServerAccessErrorKeys.NotAllowedByQuotaPlan);
    }
}
