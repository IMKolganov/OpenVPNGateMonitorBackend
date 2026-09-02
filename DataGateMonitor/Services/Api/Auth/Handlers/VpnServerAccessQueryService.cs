using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Api.Auth.Handlers;

public sealed class VpnServerAccessQueryService(
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService,
    IUserVpnServerAccessRuleQueryService userVpnServerAccessRuleQueryService)
    : IVpnServerAccessQueryService
{
    public async Task<bool> UserHasAccessAsync(int userId, int vpnServerId, CancellationToken ct)
    {
        var rule = await userVpnServerAccessRuleQueryService.GetByUserIdAndServerId(userId, vpnServerId, ct);
        if (rule is not null)
            return rule.Mode == VpnServerAccessRuleMode.Allow;

        var userQuotaPlan = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        if (userQuotaPlan is null)
            return true;

        var quotaPlanId = userQuotaPlan.QuotaPlanId;

        var allowed = await quotaPlanAllowedServerQueryService
            .GetByQuotaPlanIdAndServerId(quotaPlanId, vpnServerId, ct);

        return allowed is not null;
    }
}
