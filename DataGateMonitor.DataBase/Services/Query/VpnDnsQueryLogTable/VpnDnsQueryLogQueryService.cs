using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnDnsQuery.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnDnsQuery.Requests;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.EntityFrameworkCore;

namespace DataGateMonitor.DataBase.Services.Query.VpnDnsQueryLogTable;

public class VpnDnsQueryLogQueryService(IQueryService<VpnDnsQueryLog, int> q) : IVpnDnsQueryLogQueryService
{
    public async Task<IPagedResult<VpnDnsQueryLog>> SearchAsync(
        GetVpnDnsQueryRequest request,
        CancellationToken ct,
        IReadOnlyList<string>? profileCommonNames = null)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 500);

        var query = q.Query();

        if (request.VpnServerId > 0)
            query = query.Where(x => x.VpnServerId == request.VpnServerId);

        query = ApplyIdentityFilter(query, request.ExternalId, request.CommonName, profileCommonNames);

        if (!string.IsNullOrWhiteSpace(request.ClientIp))
            query = query.Where(x => x.ClientIp == request.ClientIp);

        if (!string.IsNullOrWhiteSpace(request.DomainContains))
        {
            var needle = request.DomainContains.Trim().ToLowerInvariant();
            query = query.Where(x => x.Domain.ToLower().Contains(needle));
        }

        if (request.FromUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc <= request.ToUtc.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.QueriedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResponse<VpnDnsQueryLog>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<IReadOnlyList<VpnDnsProfileSummaryItemDto>> GetProfileSummaryAsync(
        string externalId,
        IReadOnlyList<string> profileCommonNames,
        int vpnServerId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalId) && profileCommonNames.Count == 0)
            return Array.Empty<VpnDnsProfileSummaryItemDto>();

        var query = q.Query();
        if (vpnServerId > 0)
            query = query.Where(x => x.VpnServerId == vpnServerId);

        if (fromUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc <= toUtc.Value);

        query = ApplyIdentityFilter(query, externalId, commonName: null, profileCommonNames);

        var grouped = await query
            .GroupBy(x => new { x.CommonName, x.VpnServerId })
            .Select(g => new VpnDnsProfileSummaryItemDto
            {
                CommonName = g.Key.CommonName ?? string.Empty,
                VpnServerId = g.Key.VpnServerId,
                QueryCount = g.Count(),
                LastQueriedAtUtc = g.Max(x => x.QueriedAtUtc)
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return grouped
            .Where(x => !string.IsNullOrWhiteSpace(x.CommonName))
            .OrderByDescending(x => x.QueryCount)
            .ThenBy(x => x.CommonName, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<(int TotalCount, DateTimeOffset? LastQueriedAtUtc)> GetServerSummaryAsync(
        int vpnServerId,
        CancellationToken ct)
    {
        if (vpnServerId <= 0)
            return (0, null);

        var summary = await q.Query()
            .Where(x => x.VpnServerId == vpnServerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCount = g.Count(),
                LastQueriedAtUtc = (DateTimeOffset?)g.Max(x => x.QueriedAtUtc)
            })
            .FirstOrDefaultAsync(ct);

        if (summary is null || summary.TotalCount == 0)
            return (0, null);

        return (summary.TotalCount, summary.LastQueriedAtUtc);
    }

    public async Task<IReadOnlyList<VpnDnsTopDomainDto>> GetTopDomainsAsync(
        GetVpnDnsTopDomainsRequest request,
        CancellationToken ct)
    {
        var limit = request.Limit < 1 ? 100 : Math.Min(request.Limit, 100);

        var query = q.Query().Where(x => x.Domain != "");

        if (request.VpnServerId > 0)
            query = query.Where(x => x.VpnServerId == request.VpnServerId);

        if (!string.IsNullOrWhiteSpace(request.ExternalId))
            query = query.Where(x => x.ExternalId == request.ExternalId);

        if (request.FromUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(x => x.QueriedAtUtc <= request.ToUtc.Value);

        // Two-phase aggregation: (domain, user) then roll up. Same numbers as
        // COUNT(DISTINCT COALESCE(ExternalId, ClientIp)) but prefers HashAggregate
        // over a large Sort for DISTINCT, which spills to disk under default work_mem.
        var projected = query.Select(x => new
        {
            DomainKey = x.Domain.ToLower(),
            x.Domain,
            UserKey = x.ExternalId ?? x.ClientIp
        });

        var perUser = projected
            .GroupBy(x => new { x.DomainKey, x.UserKey })
            .Select(g => new
            {
                g.Key.DomainKey,
                Domain = g.Max(x => x.Domain),
                QueryCount = g.Count()
            });

        return await perUser
            .GroupBy(x => x.DomainKey)
            .Select(g => new VpnDnsTopDomainDto
            {
                Domain = g.Max(x => x.Domain) ?? string.Empty,
                UniqueUsersCount = g.Count(),
                QueryCount = g.Sum(x => x.QueryCount)
            })
            .OrderByDescending(x => x.UniqueUsersCount)
            .ThenByDescending(x => x.QueryCount)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    internal static IQueryable<VpnDnsQueryLog> ApplyIdentityFilter(
        IQueryable<VpnDnsQueryLog> query,
        string? externalId,
        string? commonName,
        IReadOnlyList<string>? profileCommonNames)
    {
        if (!string.IsNullOrWhiteSpace(commonName))
            return query.Where(x => x.CommonName == commonName);

        var ext = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        var cns = profileCommonNames?
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(ext) && cns is { Count: > 0 })
        {
            return query.Where(x =>
                x.ExternalId == ext
                || (x.CommonName != null && cns.Contains(x.CommonName)));
        }

        if (!string.IsNullOrWhiteSpace(ext))
            return query.Where(x => x.ExternalId == ext);

        if (cns is { Count: > 0 })
            return query.Where(x => x.CommonName != null && cns.Contains(x.CommonName));

        return query;
    }
}
