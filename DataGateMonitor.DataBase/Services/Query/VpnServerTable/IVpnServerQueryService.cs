using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerTable;

public interface IVpnServerQueryService
{
    /// <param name="requireQuotaPlanAssignment">When true, only servers present in <c>QuotaPlanAllowedServers</c> are returned. Ignored when <paramref name="restrictToQuotaPlanId"/> is set.</param>
    /// <param name="restrictToQuotaPlanId">When set, only servers linked to this quota plan in <c>QuotaPlanAllowedServers</c> are returned.</param>
    /// <param name="personalOverrides">Per-user grants added on top of the quota plan, and blocks removed from any of the sets above.</param>
    Task<List<VpnServer>> GetAll(bool includeDeleted = false, bool requireQuotaPlanAssignment = false,
        int? restrictToQuotaPlanId = null, UserVpnServerAccessOverrides? personalOverrides = null,
        CancellationToken ct = default);

    Task<VpnServer?> GetById(int id, CancellationToken ct = default);

    Task<List<VpnServer>> GetDefaultExcept(int exceptId, CancellationToken ct = default);

    Task<IPagedResult<VpnServer>> GetPage(
        int page,
        int pageSize,
        bool includeDeleted = false,
        bool requireQuotaPlanAssignment = false,
        int? restrictToQuotaPlanId = null,
        UserVpnServerAccessOverrides? personalOverrides = null,
        CancellationToken ct = default);

    Task<bool> AnyByServerName(string serverName, CancellationToken ct = default);

    Task<bool> AnyByServerNameExceptId(string serverName, int id, CancellationToken ct = default);

    /// <summary>
    /// Returns the latest <see cref="VpnServer.LastUpdate"/> for the filtered server set.
    /// Useful for lightweight cache invalidation checks.
    /// </summary>
    Task<DateTimeOffset?> GetLastUpdateStamp(
        bool includeDeleted = false,
        bool requireQuotaPlanAssignment = false,
        int? restrictToQuotaPlanId = null,
        UserVpnServerAccessOverrides? personalOverrides = null,
        CancellationToken ct = default);
}
