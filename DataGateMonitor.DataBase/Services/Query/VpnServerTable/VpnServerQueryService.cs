using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerTable;

public class VpnServerQueryService(
    IQueryService<VpnServer, int> q,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService) : IVpnServerQueryService
{
    public async Task<List<VpnServer>> GetAll(bool includeDeleted = false, bool requireQuotaPlanAssignment = false,
        int? restrictToQuotaPlanId = null, UserVpnServerAccessOverrides? personalOverrides = null,
        CancellationToken ct = default)
    {
        var overrides = personalOverrides ?? UserVpnServerAccessOverrides.None;
        var scopedIds = await ResolveScopedIdsAsync(requireQuotaPlanAssignment, restrictToQuotaPlanId, overrides, ct);
        if (scopedIds is { Count: 0 })
            return [];

        var predicate = BuildPredicate(scopedIds, overrides.DeniedVpnServerIds, includeDeleted);
        return predicate is null
            ? await q.GetAll(ct: ct)
            : await q.Where(predicate, ct: ct);
    }

    public Task<VpnServer?> GetById(int id, CancellationToken ct = default)
        => q.FindById(id, ct: ct);

    public Task<List<VpnServer>> GetDefaultExcept(int exceptId, CancellationToken ct = default)
        => q.Where(x => x.IsDefault && x.Id != exceptId && !x.IsDeleted, ct: ct);

    public async Task<IPagedResult<VpnServer>> GetPage(int page, int pageSize, bool includeDeleted = false,
        bool requireQuotaPlanAssignment = false, int? restrictToQuotaPlanId = null,
        UserVpnServerAccessOverrides? personalOverrides = null, CancellationToken ct = default)
    {
        var overrides = personalOverrides ?? UserVpnServerAccessOverrides.None;
        var scopedIds = await ResolveScopedIdsAsync(requireQuotaPlanAssignment, restrictToQuotaPlanId, overrides, ct);
        if (scopedIds is { Count: 0 })
        {
            return new PagedResponse<VpnServer>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                Items = []
            };
        }

        return await q.Page(page, pageSize,
            predicate: BuildPredicate(scopedIds, overrides.DeniedVpnServerIds, includeDeleted), ct: ct);
    }

    public Task<bool> AnyByServerName(string serverName, CancellationToken ct = default)
        => q.Any(x => x.ServerName == serverName && !x.IsDeleted, ct: ct);

    public Task<bool> AnyByServerNameExceptId(string serverName, int id, CancellationToken ct = default)
        => q.Any(x => x.ServerName == serverName && x.Id != id && !x.IsDeleted, ct: ct);

    public async Task<DateTimeOffset?> GetLastUpdateStamp(bool includeDeleted = false,
        bool requireQuotaPlanAssignment = false, int? restrictToQuotaPlanId = null,
        UserVpnServerAccessOverrides? personalOverrides = null, CancellationToken ct = default)
    {
        var overrides = personalOverrides ?? UserVpnServerAccessOverrides.None;
        var scopedIds = await ResolveScopedIdsAsync(requireQuotaPlanAssignment, restrictToQuotaPlanId, overrides, ct);
        if (scopedIds is { Count: 0 })
            return null;

        var query = q.Query();
        if (BuildPredicate(scopedIds, overrides.DeniedVpnServerIds, includeDeleted) is { } predicate)
            query = query.Where(predicate);

        return await query.MaxAsync(x => (DateTimeOffset?)x.LastUpdate, ct);
    }

    /// <returns>Server ids the caller is limited to, or <c>null</c> when only personal blocks narrow the full list.</returns>
    private async Task<HashSet<int>?> ResolveScopedIdsAsync(
        bool requireQuotaPlanAssignment,
        int? restrictToQuotaPlanId,
        UserVpnServerAccessOverrides overrides,
        CancellationToken ct)
    {
        HashSet<int>? scopedIds = null;

        if (restrictToQuotaPlanId is int planId)
        {
            scopedIds = await quotaPlanAllowedServerQueryService.GetVpnServerIdsByQuotaPlanId(planId, ct);
            scopedIds.UnionWith(overrides.AllowedVpnServerIds);
        }
        else if (requireQuotaPlanAssignment)
        {
            scopedIds = await quotaPlanAllowedServerQueryService.GetDistinctVpnServerIds(ct);
        }

        scopedIds?.ExceptWith(overrides.DeniedVpnServerIds);
        return scopedIds;
    }

    /// <returns><c>null</c> when nothing narrows the query, so the caller can keep the unfiltered fast path.</returns>
    private static Expression<Func<VpnServer, bool>>? BuildPredicate(
        HashSet<int>? scopedIds,
        HashSet<int> deniedIds,
        bool includeDeleted)
    {
        if (scopedIds is not null)
        {
            return includeDeleted
                ? x => scopedIds.Contains(x.Id)
                : x => scopedIds.Contains(x.Id) && !x.IsDeleted;
        }

        if (deniedIds.Count > 0)
        {
            return includeDeleted
                ? x => !deniedIds.Contains(x.Id)
                : x => !deniedIds.Contains(x.Id) && !x.IsDeleted;
        }

        return includeDeleted
            ? null
            : x => !x.IsDeleted;
    }
}
