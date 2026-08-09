using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// Overview totals over sessions and traffic (traffic aggregated in PostgreSQL or in-memory for tests).
/// </summary>
public sealed class OpenVpnOverviewTotalsQuery(
    IUnitOfWork uow,
    IOverviewTrafficAggregator trafficAggregator) : IOpenVpnOverviewTotalsQuery
{
    public Task<OverviewTotalsResponse> GetOverviewTotalsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct = default)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var key = string.Join('|',
            "overview-summary",
            fromUtc.UtcTicks,
            toUtc.UtcTicks,
            vpnServerId?.ToString() ?? "-",
            externalId ?? "-");

        return OverviewTrafficResultCache.GetOrCreateAsync(
            key,
            token => BuildOverviewTotalsAsync(fromUtc, toUtc, vpnServerId, externalId, token),
            ct);
    }

    private async Task<OverviewTotalsResponse> BuildOverviewTotalsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        var sessionsQ = uow.GetQuery<VpnServerClient>().AsQueryable();

        if (vpnServerId.HasValue)
            sessionsQ = sessionsQ.Where(x => x.VpnServerId == vpnServerId.Value);

        if (!string.IsNullOrWhiteSpace(externalId))
            sessionsQ = sessionsQ.Where(x => x.ExternalId == externalId!);

        sessionsQ = sessionsQ
            .Where(x => x.ConnectedSince >= fromUtc && x.ConnectedSince < toUtc)
            .AsNoTracking();

        // One round-trip: sessions + distinct users (same filters as before).
        var sessionAgg = await sessionsQ
            .GroupBy(_ => 1)
            .Select(g => new
            {
                SessionsCount = g.LongCount(),
                UsersCount = g
                    .Where(x => x.ExternalId != null && x.ExternalId != "")
                    .Select(x => x.ExternalId)
                    .Distinct()
                    .LongCount()
            })
            .FirstOrDefaultAsync(ct);

        var sessionsCount = sessionAgg?.SessionsCount ?? 0L;
        var usersCount = sessionAgg?.UsersCount ?? 0L;

        var trafficTotals = await trafficAggregator.GetTrafficTotalsAsync(
            fromUtc, toUtc, vpnServerId, externalId, ct);

        return new OverviewTotalsResponse
        {
            Meta = new OverviewMetaDto
            {
                From = fromUtc,
                To = toUtc,
                Grouping = "none",
                Timezone = "UTC",
                TrafficUnit = "bytes",
                VpnServerId = vpnServerId
            },
            Totals = new TotalsPayloadDto
            {
                SessionsCount = sessionsCount,
                UsersCount = usersCount,
                TrafficInBytes = trafficTotals.TrafficInBytes,
                TrafficOutBytes = trafficTotals.TrafficOutBytes
            }
        };
    }
}
