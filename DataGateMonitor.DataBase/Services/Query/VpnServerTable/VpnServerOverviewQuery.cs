using Mapster;
using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerTable;

public class VpnServerOverviewQuery(IUnitOfWork uow) : IVpnServerOverviewQuery
{
    // Set-based aggregates (same fields/semantics as former correlated subqueries)
    public async Task<List<VpnServerWithStatusDto>> GetAllVpnServersWithStatusAsync(
        bool includeDeleted = false,
        bool requireQuotaPlanAssignment = false,
        int? restrictToQuotaPlanId = null,
        UserVpnServerAccessOverrides? personalOverrides = null,
        CancellationToken ct = default)
    {
        var overrides = personalOverrides ?? UserVpnServerAccessOverrides.None;
        var serversBase = uow.GetQuery<VpnServer>().AsQueryable();
        var servers = includeDeleted ? serversBase : serversBase.Where(s => !s.IsDeleted);
        if (restrictToQuotaPlanId is int pid)
        {
            var allowed = uow.GetQuery<QuotaPlanAllowedServer>().AsQueryable();
            var personallyAllowed = overrides.AllowedVpnServerIds;
            servers = servers.Where(s => allowed.Any(a => a.VpnServerId == s.Id && a.QuotaPlanId == pid)
                                         || personallyAllowed.Contains(s.Id));
        }
        else if (requireQuotaPlanAssignment)
        {
            var allowed = uow.GetQuery<QuotaPlanAllowedServer>().AsQueryable();
            servers = servers.Where(s => allowed.Any(a => a.VpnServerId == s.Id));
        }

        if (overrides.DeniedVpnServerIds.Count > 0)
        {
            var denied = overrides.DeniedVpnServerIds;
            servers = servers.Where(s => !denied.Contains(s.Id));
        }

        var serverList = await servers.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);
        if (serverList.Count == 0)
            return [];

        return await ComposeWithStatusAsync(serverList, ct);
    }

    // Single server variant; throws if not found
    public async Task<VpnServerWithStatusDto> GetVpnServerWithStatusAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await uow.GetQuery<VpnServer>().AsQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == vpnServerId, ct);
        if (server is null)
            throw new NullReferenceException("OpenVPN Server not found");

        var list = await ComposeWithStatusAsync([server], ct);
        return list[0];
    }

    public async Task<(int CountConnectedClients, int CountSessions)> GetClientCountersAsync(
        int vpnServerId, CancellationToken ct)
    {
        var clients = uow.GetQuery<VpnServerClient>().AsQueryable();

        var counters = await clients
            .Where(c => c.VpnServerId == vpnServerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                CountConnectedClients = g.Count(c => c.IsConnected),
                CountSessions = g.Count()
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return counters is null
            ? (0, 0)
            : (counters.CountConnectedClients, counters.CountSessions);
    }

    private async Task<List<VpnServerWithStatusDto>> ComposeWithStatusAsync(
        List<VpnServer> serverList,
        CancellationToken ct)
    {
        var serverIds = serverList.Select(s => s.Id).ToList();
        var clients = uow.GetQuery<VpnServerClient>().AsQueryable();
        var logs = uow.GetQuery<VpnServerStatusLog>().AsQueryable();

        var clientAggs = await clients
            .AsNoTracking()
            .Where(c => serverIds.Contains(c.VpnServerId))
            .GroupBy(c => c.VpnServerId)
            .Select(g => new
            {
                VpnServerId = g.Key,
                CountConnectedClients = g.Count(c => c.IsConnected),
                CountSessions = g.Count()
            })
            .ToListAsync(ct);

        var logTotals = await logs
            .AsNoTracking()
            .Where(l => serverIds.Contains(l.VpnServerId))
            .GroupBy(l => l.VpnServerId)
            .Select(g => new
            {
                VpnServerId = g.Key,
                TotalBytesIn = g.Sum(l => (long?)l.BytesIn) ?? 0L,
                TotalBytesOut = g.Sum(l => (long?)l.BytesOut) ?? 0L
            })
            .ToListAsync(ct);

        var latestLogIds = await logs
            .AsNoTracking()
            .Where(l => serverIds.Contains(l.VpnServerId))
            .GroupBy(l => l.VpnServerId)
            .Select(g => g.Max(l => l.Id))
            .ToListAsync(ct);

        var latestLogs = latestLogIds.Count == 0
            ? []
            : await logs
                .AsNoTracking()
                .Where(l => latestLogIds.Contains(l.Id))
                .ToListAsync(ct);

        var clientsByServer = clientAggs.ToDictionary(x => x.VpnServerId);
        var totalsByServer = logTotals.ToDictionary(x => x.VpnServerId);
        var latestByServer = latestLogs.ToDictionary(l => l.VpnServerId);

        var result = new List<VpnServerWithStatusDto>(serverList.Count);
        foreach (var s in serverList)
        {
            clientsByServer.TryGetValue(s.Id, out var clientAgg);
            totalsByServer.TryGetValue(s.Id, out var totals);
            latestByServer.TryGetValue(s.Id, out var latest);

            result.Add(new VpnServerWithStatusDto
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = s.Adapt<VpnServerDto>(),
                },
                CountConnectedClients = clientAgg?.CountConnectedClients ?? 0,
                CountSessions = clientAgg?.CountSessions ?? 0,
                TotalBytesIn = totals?.TotalBytesIn ?? 0L,
                TotalBytesOut = totals?.TotalBytesOut ?? 0L,
                VpnServerStatusLogResponse = latest is null
                    ? null
                    : new VpnServerStatusLogResponse
                    {
                        VpnServerId = latest.VpnServerId,
                        SessionId = latest.SessionId,
                        UpSince = latest.UpSince,
                        ServerLocalIp = latest.ServerLocalIp,
                        ServerRemoteIp = latest.ServerRemoteIp,
                        BytesIn = latest.BytesIn,
                        BytesOut = latest.BytesOut,
                        Version = latest.Version
                    },
                InstalledManagerVersion = string.IsNullOrWhiteSpace(s.ManagerVersion)
                    ? null
                    : s.ManagerVersion.Trim()
            });
        }

        return result;
    }
}
