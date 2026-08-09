using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

public sealed class OverviewTrafficDailyRollupService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : IOverviewTrafficDailyRollupService
{
    public async Task<int> RollupDayAsync(DateOnly dayUtc, CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        if (!ctx.Database.IsNpgsql())
            return 0;

        var trafficTable = ResolveTable<VpnServerClientTraffic>(ctx);
        var dailyTable = ResolveTable<VpnServerClientTrafficDaily>(ctx);
        var dayStart = dayUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var now = DateTimeOffset.UtcNow;

        var sql = $"""
                   INSERT INTO {dailyTable}
                       ("VpnServerId", "UserId", "ExternalId", "SessionId", "DayUtc",
                        "TrafficInBytes", "TrafficOutBytes", "SampleCount", "CreateDate", "LastUpdate")
                   SELECT
                       agg."VpnServerId",
                       agg."UserId",
                       agg."ExternalId",
                       agg."SessionId",
                       @dayUtc::date,
                       agg.d_in,
                       agg.d_out,
                       agg.sample_count,
                       @now,
                       @now
                   FROM (
                       WITH filtered AS (
                           SELECT
                               t."VpnServerId",
                               t."UserId",
                               t."ExternalId",
                               t."SessionId",
                               t."MeasuredAt",
                               t."BytesReceived",
                               t."BytesSent"
                           FROM {trafficTable} t
                           WHERE t."MeasuredAt" >= @dayStart
                             AND t."MeasuredAt" < @dayEnd
                       ),
                       baselines AS (
                           SELECT DISTINCT ON (t."SessionId")
                               t."SessionId",
                               t."BytesReceived" AS baseline_in,
                               t."BytesSent" AS baseline_out
                           FROM {trafficTable} t
                           WHERE t."MeasuredAt" < @dayStart
                             AND t."SessionId" IN (SELECT DISTINCT f."SessionId" FROM filtered f)
                           ORDER BY t."SessionId", t."MeasuredAt" DESC
                       ),
                       with_prev AS (
                           SELECT
                               f."VpnServerId",
                               f."UserId",
                               f."ExternalId",
                               f."SessionId",
                               f."BytesReceived",
                               f."BytesSent",
                               LAG(f."BytesReceived") OVER w AS prev_in,
                               LAG(f."BytesSent") OVER w AS prev_out,
                               b.baseline_in,
                               b.baseline_out
                           FROM filtered f
                           LEFT JOIN baselines b ON b."SessionId" = f."SessionId"
                           WINDOW w AS (PARTITION BY f."SessionId" ORDER BY f."MeasuredAt")
                       ),
                       filtered_counts AS (
                           SELECT f."VpnServerId", f."SessionId", COUNT(*)::int AS sample_count
                           FROM filtered f
                           GROUP BY f."VpnServerId", f."SessionId"
                       ),
                       row_deltas AS (
                           SELECT
                               wp."VpnServerId",
                               wp."UserId",
                               wp."ExternalId",
                               wp."SessionId",
                               CASE
                                   WHEN wp.prev_in IS NOT NULL THEN
                                       CASE WHEN wp."BytesReceived" >= wp.prev_in
                                            THEN wp."BytesReceived" - wp.prev_in
                                            ELSE wp."BytesReceived" END
                                   WHEN wp.baseline_in IS NOT NULL THEN
                                       CASE WHEN wp."BytesReceived" >= wp.baseline_in
                                            THEN wp."BytesReceived" - wp.baseline_in
                                            ELSE wp."BytesReceived" END
                                   ELSE 0::bigint
                               END AS d_in,
                               CASE
                                   WHEN wp.prev_out IS NOT NULL THEN
                                       CASE WHEN wp."BytesSent" >= wp.prev_out
                                            THEN wp."BytesSent" - wp.prev_out
                                            ELSE wp."BytesSent" END
                                   WHEN wp.baseline_out IS NOT NULL THEN
                                       CASE WHEN wp."BytesSent" >= wp.baseline_out
                                            THEN wp."BytesSent" - wp.baseline_out
                                            ELSE wp."BytesSent" END
                                   ELSE 0::bigint
                               END AS d_out
                           FROM with_prev wp
                           WHERE wp.prev_in IS NOT NULL OR wp.baseline_in IS NOT NULL
                       )
                       SELECT
                           rd."VpnServerId",
                           MAX(rd."UserId") AS "UserId",
                           MAX(rd."ExternalId") AS "ExternalId",
                           rd."SessionId",
                           COALESCE(SUM(GREATEST(rd.d_in, 0)), 0)::bigint AS d_in,
                           COALESCE(SUM(GREATEST(rd.d_out, 0)), 0)::bigint AS d_out,
                           MAX(fc.sample_count)::int AS sample_count
                       FROM row_deltas rd
                       INNER JOIN filtered_counts fc
                           ON fc."VpnServerId" = rd."VpnServerId" AND fc."SessionId" = rd."SessionId"
                       GROUP BY rd."VpnServerId", rd."SessionId"
                   ) agg
                   ON CONFLICT ("VpnServerId", "SessionId", "DayUtc")
                   DO UPDATE SET
                       "UserId" = EXCLUDED."UserId",
                       "ExternalId" = EXCLUDED."ExternalId",
                       "TrafficInBytes" = EXCLUDED."TrafficInBytes",
                       "TrafficOutBytes" = EXCLUDED."TrafficOutBytes",
                       "SampleCount" = EXCLUDED."SampleCount",
                       "LastUpdate" = EXCLUDED."LastUpdate"
                   """;

        var affected = await ctx.Database.ExecuteSqlRawAsync(
            sql,
            [
                new NpgsqlParameter("dayUtc", NpgsqlDbType.Date) { Value = dayUtc },
                new NpgsqlParameter("dayStart", NpgsqlDbType.TimestampTz) { Value = dayStart },
                new NpgsqlParameter("dayEnd", NpgsqlDbType.TimestampTz) { Value = dayEnd },
                new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now.UtcDateTime },
            ],
            ct);

        return affected;
    }

    public async Task<int> BackfillRangeAsync(DateOnly fromDayUtc, DateOnly toDayUtc, CancellationToken ct = default)
    {
        if (toDayUtc < fromDayUtc)
            (fromDayUtc, toDayUtc) = (toDayUtc, fromDayUtc);

        var total = 0;
        for (var day = fromDayUtc; day <= toDayUtc; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            total += await RollupDayAsync(day, ct);
        }

        return total;
    }

    public async Task<IReadOnlyList<DateOnly>> GetMissingRollupDaysAsync(
        DateOnly throughDayUtc,
        CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        if (!ctx.Database.IsNpgsql())
            return [];

        if (!await ctx.VpnServerClientTraffics.AsNoTracking().AnyAsync(ct))
            return [];

        var trafficTable = ResolveTable<VpnServerClientTraffic>(ctx);
        var dailyTable = ResolveTable<VpnServerClientTrafficDaily>(ctx);

        // Same set as DISTINCT date_trunc(MeasuredAt) anti-join dailies, without scanning all traffic rows:
        // calendar days from first sample through @throughDay that have traffic but no daily rollup
        // (including gaps in the middle). Uses IX_ClientTraffic_At + IX_ClientTrafficDaily_Day_*.
        var sql = $"""
                   WITH bounds AS (
                       SELECT ("MeasuredAt" AT TIME ZONE 'UTC')::date AS mn
                       FROM {trafficTable}
                       ORDER BY "MeasuredAt"
                       LIMIT 1
                   ),
                   cand AS (
                       SELECT gs::date AS day_utc
                       FROM bounds b
                       CROSS JOIN LATERAL generate_series(b.mn, @throughDay, interval '1 day') AS gs
                       WHERE b.mn <= @throughDay
                   )
                   SELECT c.day_utc AS "Value"
                   FROM cand c
                   WHERE NOT EXISTS (
                       SELECT 1 FROM {dailyTable} d WHERE d."DayUtc" = c.day_utc
                   )
                     AND EXISTS (
                       SELECT 1
                       FROM {trafficTable} t
                       WHERE t."MeasuredAt" >= (c.day_utc::timestamp AT TIME ZONE 'UTC')
                         AND t."MeasuredAt" <  ((c.day_utc + 1)::timestamp AT TIME ZONE 'UTC')
                   )
                   ORDER BY c.day_utc
                   """;

        return await ctx.Database
            .SqlQueryRaw<DateOnly>(sql, new NpgsqlParameter("throughDay", NpgsqlDbType.Date) { Value = throughDayUtc })
            .ToListAsync(ct);
    }

    public async Task<TrafficDailyRollupCatchUpResult> CatchUpMissingDaysAsync(
        DateOnly throughDayUtc,
        CancellationToken ct = default)
    {
        var missing = await GetMissingRollupDaysAsync(throughDayUtc, ct);
        if (missing.Count == 0)
            return TrafficDailyRollupCatchUpResult.Empty;

        var processed = new List<DateOnly>(missing.Count);
        var totalRows = 0;
        foreach (var day in missing)
        {
            ct.ThrowIfCancellationRequested();
            totalRows += await RollupDayAsync(day, ct);
            processed.Add(day);
        }

        return new TrafficDailyRollupCatchUpResult(processed, totalRows);
    }

    public async Task<(DateOnly? FirstRawDay, DateOnly? LastRolledUpDay)> GetCoverageAsync(CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        if (!await ctx.VpnServerClientTraffics.AsNoTracking().AnyAsync(ct))
            return (null, null);

        var firstMeasured = await ctx.VpnServerClientTraffics.AsNoTracking()
            .MinAsync(t => t.MeasuredAt, ct);
        var firstRaw = DateOnly.FromDateTime(firstMeasured.UtcDateTime);

        var lastDaily = await ctx.VpnServerClientTrafficDailies.AsNoTracking()
            .Select(t => (DateOnly?)t.DayUtc)
            .MaxAsync(ct);

        return (firstRaw, lastDaily);
    }

    private static string ResolveTable<T>(ApplicationDbContext ctx) where T : class
    {
        var entity = ctx.Model.FindEntityType(typeof(T))
                     ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped.");
        var schema = entity.GetSchema() ?? "public";
        var table = entity.GetTableName() ?? throw new InvalidOperationException("Table name missing.");
        return $"\"{schema}\".\"{table}\"";
    }
}
