using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// Query service that returns aggregated map points from VpnServerClient
/// filtered by time range and optional server/external id.
/// Each DB row is a session; rows are grouped by (Country, Region, Latitude, Longitude).
/// </summary>
public sealed class OpenVpnGeoQueryService(IUnitOfWork uow) : IOpenVpnGeoQueryService
{
    public async Task<OverviewPointsResponse> GetGeoPointsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId = null,
        string? externalId = null,
        bool onlyWithCoordinates = true,
        CancellationToken ct = default)
    {
        // Normalize bounds (inclusive start, exclusive end) and clamp future To (apps send month-end).
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);
        var utcNow = DateTimeOffset.UtcNow;
        if (fromUtc > utcNow)
            fromUtc = toUtc = utcNow;
        else if (toUtc > utcNow)
            toUtc = utcNow;

        // Base query
        var q = uow.GetQuery<VpnServerClient>().AsQueryable();

        // Time range filter on session start
        var from = fromUtc.UtcDateTime;
        var to = toUtc.UtcDateTime;

        q = q.Where(s => s.ConnectedSince >= from && s.ConnectedSince < to);

        // Optional filters
        if (vpnServerId.HasValue)
            q = q.Where(s => s.VpnServerId == vpnServerId.Value);

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var ex = externalId.Trim();
            q = q.Where(s => s.ExternalId == ex);
            // For PostgreSQL case-insensitive:
            // q = q.Where(s => EF.Functions.ILike(s.ExternalId, ex));
        }

        if (onlyWithCoordinates)
        {
            q = q.Where(s => s.Latitude != null &&
                             s.Longitude != null &&
                             !(s.Latitude == 0 && s.Longitude == 0));
        }

        q = q.AsNoTracking();

        // Aggregate by Country/Region/Lat/Lon
        var rows = await q
            .GroupBy(s => new { s.Country, s.Region, s.Latitude, s.Longitude })
            .Select(g => new
            {
                g.Key.Country,
                g.Key.Region,
                g.Key.Latitude,
                g.Key.Longitude,
                SessionsCount = g.Count(),
                TotalBytesIn = g.Sum(x => (long?)x.BytesReceived) ?? 0,
                TotalBytesOut = g.Sum(x => (long?)x.BytesSent) ?? 0
            })
            .OrderBy(x => x.Country)
            .ThenBy(x => x.Region)
            .ThenBy(x => x.Latitude)
            .ThenBy(x => x.Longitude)
            .ToListAsync(ct);

        var points = rows.Select(x =>
            new GeoPointAggDto()
            {
                Country = x.Country,
                Region = x.Region,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                SessionsCount = x.SessionsCount,
                TotalBytesIn = x.TotalBytesIn,
                TotalBytesOut = x.TotalBytesOut,
            }).ToList();

        return new OverviewPointsResponse { GeoPointAggs = points };
    }
}