using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;

namespace DataGateMonitor.Services.VpnAccess;

public interface IVpnServerQuotaPlanAccessGuard
{
    /// <summary>
    /// Ensures the dashboard user linked to <paramref name="externalId"/> may use <paramref name="vpnServerId"/>.
    /// Dashboard admins bypass the quota-plan allowlist. Users without a link or without an active plan are allowed.
    /// </summary>
    Task EnsureTargetUserMayUseServerAsync(string? externalId, int vpnServerId, CancellationToken ct);
}

public sealed class VpnServerQuotaPlanAccessGuard(
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService,
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
