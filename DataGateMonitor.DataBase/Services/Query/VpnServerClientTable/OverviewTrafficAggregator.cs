using Microsoft.EntityFrameworkCore;
using Npgsql;
using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// Aggregates VpnServerClientTraffic in PostgreSQL (production) or in-memory (unit tests).
/// Avoids loading all poll samples into the backend process.
/// </summary>
public sealed class OverviewTrafficAggregator(
    IUnitOfWork uow,
    IDbContextFactory<ApplicationDbContext>? dbContextFactory = null) : IOverviewTrafficAggregator
{
    public Task<IReadOnlyList<OverviewTrafficBucketRow>> GetTrafficSeriesBucketsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct = default)
        => UsePostgresAsync(ct)
            ? QueryPostgresAsync(ct, ctx => QueryTrafficSeriesHybridAsync(
                ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct))
            : Task.FromResult<IReadOnlyList<OverviewTrafficBucketRow>>(
                AggregateTrafficSeriesBucketsInMemory(fromUtc, toUtc, grouping, vpnServerId, externalId));

    public Task<IReadOnlyList<OverviewUsersSeriesBucketRow>> GetUsersSeriesBucketsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct = default)
        => UsePostgresAsync(ct)
            ? QueryPostgresAsync(ct, ctx => QueryUsersSeriesHybridAsync(
                ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct))
            : Task.FromResult<IReadOnlyList<OverviewUsersSeriesBucketRow>>(
                AggregateUsersSeriesBucketsInMemory(fromUtc, toUtc, grouping, vpnServerId, externalId));

    public async Task<OverviewTrafficTotalsRow> GetTrafficTotalsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct = default)
    {
        if (UsePostgresAsync(ct))
            return await QueryPostgresAsync(ct, ctx => QueryTrafficTotalsHybridAsync(
                ctx, fromUtc, toUtc, vpnServerId, externalId, ct));

        return AggregateTrafficTotalsInMemory(fromUtc, toUtc, vpnServerId, externalId);
    }

    public Task<IReadOnlyList<OverviewUserTrafficRow>> GetUserTrafficRowsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct = default)
        => UsePostgresAsync(ct)
            ? QueryPostgresAsync(ct, ctx => QueryUserTrafficHybridAsync(
                ctx, fromUtc, toUtc, vpnServerId, externalId, ct))
            : Task.FromResult<IReadOnlyList<OverviewUserTrafficRow>>(
                AggregateUserTrafficRowsInMemory(fromUtc, toUtc, vpnServerId, externalId));

    private bool UsePostgresAsync(CancellationToken ct) => dbContextFactory is not null;

    private async Task<T> QueryPostgresAsync<T>(
        CancellationToken ct,
        Func<ApplicationDbContext, Task<T>> query)
    {
        await using var ctx = await dbContextFactory!.CreateDbContextAsync(ct);
        if (!ctx.Database.IsNpgsql())
            throw new InvalidOperationException("PostgreSQL aggregation requires Npgsql provider.");

        return await query(ctx);
    }

    private static string ResolveTrafficTable(ApplicationDbContext ctx)
    {
        var entity = ctx.Model.FindEntityType(typeof(VpnServerClientTraffic))
                     ?? throw new InvalidOperationException("VpnServerClientTraffic entity not mapped.");
        var schema = entity.GetSchema() ?? "public";
        var table = entity.GetTableName() ?? throw new InvalidOperationException("Traffic table name missing.");
        return $"\"{schema}\".\"{table}\"";
    }

    private static NpgsqlParameter[] BuildFilterParams(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan offset,
        int? vpnServerId,
        string? externalId)
        => OverviewTrafficPostgresSql.BuildFilterParams(fromUtc, toUtc, offset, vpnServerId, externalId);

    private static string BuildFilteredTrafficCte(string table, int? vpnServerId, string? externalId)
        => OverviewTrafficPostgresSql.BuildFilteredTrafficCte(table, vpnServerId, externalId);

    private static string BuildBaselinesCte(string table, int? vpnServerId, string? externalId)
        => OverviewTrafficPostgresSql.BuildBaselinesCte(table, vpnServerId, externalId);

    private static string BucketStartSql(OverviewGrouping grouping)
        => OverviewTrafficPostgresSql.BucketStartSql(grouping);

    private static void NormalizeTrafficRange(ref DateTimeOffset fromUtc, ref DateTimeOffset toUtc)
        => (fromUtc, toUtc) = OverviewTrafficPostgresSql.NormalizeTrafficRange(
            fromUtc, toUtc, DateTimeOffset.UtcNow);

    private async Task<IReadOnlyList<OverviewTrafficBucketRow>> QueryTrafficSeriesHybridAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        NormalizeTrafficRange(ref fromUtc, ref toUtc);

        if (!OverviewTrafficDailyQueries.SupportsDailyGrouping(grouping))
            return await QueryTrafficSeriesBucketsPostgresAsync(ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct);

        var split = OverviewTrafficDailyQueries.SplitRange(fromUtc, toUtc);
        var hasDaily = await OverviewTrafficDailyQueries.HasDailyRowsAsync(ctx, split.DailyFrom, split.DailyToExclusive, ct);
        if (!hasDaily)
            return await QueryTrafficSeriesBucketsPostgresAsync(ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct);

        IReadOnlyList<OverviewTrafficBucketRow> dailyRows = [];
        if (split.DailyToExclusive > split.DailyFrom)
            dailyRows = await OverviewTrafficDailyQueries.QuerySeriesAsync(
                ctx, split.DailyFrom, split.DailyToExclusive, grouping, vpnServerId, externalId, ct);

        if (split.RawFromUtc is not { } rawFrom || split.RawToUtc is not { } rawTo || rawTo <= rawFrom)
            return dailyRows;

        var rawRows = await QueryTrafficSeriesBucketsPostgresAsync(
            ctx, rawFrom, rawTo, grouping, vpnServerId, externalId, ct);
        return OverviewTrafficDailyQueries.MergeBuckets(dailyRows, rawRows, fromUtc.Offset);
    }

    private async Task<IReadOnlyList<OverviewUsersSeriesBucketRow>> QueryUsersSeriesHybridAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        NormalizeTrafficRange(ref fromUtc, ref toUtc);

        if (!OverviewTrafficDailyQueries.SupportsDailyGrouping(grouping))
            return await QueryUsersSeriesBucketsPostgresAsync(ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct);

        var split = OverviewTrafficDailyQueries.SplitRange(fromUtc, toUtc);
        var hasDaily = await OverviewTrafficDailyQueries.HasDailyRowsAsync(ctx, split.DailyFrom, split.DailyToExclusive, ct);
        if (!hasDaily)
            return await QueryUsersSeriesBucketsPostgresAsync(ctx, fromUtc, toUtc, grouping, vpnServerId, externalId, ct);

        IReadOnlyList<OverviewUsersSeriesBucketRow> dailyRows = [];
        if (split.DailyToExclusive > split.DailyFrom)
            dailyRows = await OverviewTrafficDailyQueries.QueryUsersSeriesAsync(
                ctx, split.DailyFrom, split.DailyToExclusive, grouping, vpnServerId, externalId, ct);

        if (split.RawFromUtc is not { } rawFrom || split.RawToUtc is not { } rawTo || rawTo <= rawFrom)
            return dailyRows;

        var rawRows = await QueryUsersSeriesBucketsPostgresAsync(
            ctx, rawFrom, rawTo, grouping, vpnServerId, externalId, ct);
        return OverviewTrafficDailyQueries.MergeUsersSeriesBuckets(dailyRows, rawRows, fromUtc.Offset);
    }

    private async Task<OverviewTrafficTotalsRow> QueryTrafficTotalsHybridAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        NormalizeTrafficRange(ref fromUtc, ref toUtc);

        var split = OverviewTrafficDailyQueries.SplitRange(fromUtc, toUtc);
        var hasDaily = await OverviewTrafficDailyQueries.HasDailyRowsAsync(ctx, split.DailyFrom, split.DailyToExclusive, ct);
        if (!hasDaily)
        {
            var rows = await QueryTrafficTotalsPostgresAsync(ctx, fromUtc, toUtc, vpnServerId, externalId, ct);
            return rows[0];
        }

        var total = new OverviewTrafficTotalsRow();
        if (split.DailyToExclusive > split.DailyFrom)
        {
            total = await OverviewTrafficDailyQueries.QueryTotalsAsync(
                ctx, split.DailyFrom, split.DailyToExclusive, vpnServerId, externalId, ct);
        }

        if (split.RawFromUtc is { } rawFrom && split.RawToUtc is { } rawTo && rawTo > rawFrom)
        {
            var rawRows = await QueryTrafficTotalsPostgresAsync(ctx, rawFrom, rawTo, vpnServerId, externalId, ct);
            total = OverviewTrafficDailyQueries.CombineTotals(total, rawRows[0]);
        }

        return total;
    }

    private async Task<IReadOnlyList<OverviewUserTrafficRow>> QueryUserTrafficHybridAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        NormalizeTrafficRange(ref fromUtc, ref toUtc);

        var split = OverviewTrafficDailyQueries.SplitRange(fromUtc, toUtc);
        var hasDaily = await OverviewTrafficDailyQueries.HasDailyRowsAsync(ctx, split.DailyFrom, split.DailyToExclusive, ct);
        if (!hasDaily)
            return await QueryUserTrafficRowsPostgresAsync(ctx, fromUtc, toUtc, vpnServerId, externalId, ct);

        IReadOnlyList<OverviewUserTrafficRow> dailyRows = [];
        if (split.DailyToExclusive > split.DailyFrom)
        {
            dailyRows = await OverviewTrafficDailyQueries.QueryUserTrafficAsync(
                ctx, split.DailyFrom, split.DailyToExclusive, vpnServerId, externalId, ct);
        }

        if (split.RawFromUtc is not { } rawFrom || split.RawToUtc is not { } rawTo || rawTo <= rawFrom)
            return dailyRows;

        var rawRows = await QueryUserTrafficRowsPostgresAsync(ctx, rawFrom, rawTo, vpnServerId, externalId, ct);
        return OverviewTrafficDailyQueries.MergeUserTrafficRows(dailyRows, rawRows);
    }

    private async Task<IReadOnlyList<OverviewTrafficBucketRow>> QueryTrafficSeriesBucketsPostgresAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var table = ResolveTrafficTable(ctx);
        var bucketExpr = BucketStartSql(grouping);
        var sql = $"""
                   WITH {BuildFilteredTrafficCte(table, vpnServerId, externalId)},
                   with_prev AS (
                       SELECT
                           f."SessionId",
                           f."MeasuredAt",
                           CASE
                               WHEN LAG(f."BytesReceived") OVER w IS NULL THEN 0::bigint
                               WHEN f."BytesReceived" >= LAG(f."BytesReceived") OVER w
                                   THEN f."BytesReceived" - LAG(f."BytesReceived") OVER w
                               ELSE f."BytesReceived"
                           END AS d_in,
                           CASE
                               WHEN LAG(f."BytesSent") OVER w IS NULL THEN 0::bigint
                               WHEN f."BytesSent" >= LAG(f."BytesSent") OVER w
                                   THEN f."BytesSent" - LAG(f."BytesSent") OVER w
                               ELSE f."BytesSent"
                           END AS d_out
                       FROM filtered f
                       WINDOW w AS (PARTITION BY f."SessionId" ORDER BY f."MeasuredAt")
                   ),
                   bucketed AS (
                       SELECT
                           {bucketExpr.Replace("\"MeasuredAt\"", "wp.\"MeasuredAt\"")} AS bucket_ts,
                           wp."SessionId",
                           wp.d_in,
                           wp.d_out
                       FROM with_prev wp
                   )
                   SELECT
                       b.bucket_ts AS "BucketTs",
                       COUNT(DISTINCT b."SessionId")::int AS "ActiveClients",
                       COALESCE(SUM(b.d_in), 0)::bigint AS "TrafficInBytes",
                       COALESCE(SUM(b.d_out), 0)::bigint AS "TrafficOutBytes"
                   FROM bucketed b
                   GROUP BY b.bucket_ts
                   ORDER BY b.bucket_ts
                   """;

        var rows = await ctx.Database
            .SqlQueryRaw<OverviewTrafficBucketRow>(
                sql,
                BuildFilterParams(fromUtc, toUtc, fromUtc.Offset, vpnServerId, externalId))
            .ToListAsync(ct);

        return rows;
    }

    private async Task<IReadOnlyList<OverviewUsersSeriesBucketRow>> QueryUsersSeriesBucketsPostgresAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var table = ResolveTrafficTable(ctx);
        var bucketExpr = BucketStartSql(grouping);
        var sql = $"""
                   WITH {BuildFilteredTrafficCte(table, vpnServerId, externalId)},
                   bucketed AS (
                       SELECT
                           {bucketExpr.Replace("\"MeasuredAt\"", "f.\"MeasuredAt\"")} AS bucket_ts,
                           f."SessionId",
                           NULLIF(f."ExternalId", '') AS external_id
                       FROM filtered f
                   )
                   SELECT
                       b.bucket_ts AS "BucketTs",
                       COUNT(DISTINCT b."SessionId")::int AS "ActiveSessions",
                       COUNT(DISTINCT b.external_id)::int AS "ActiveUsers"
                   FROM bucketed b
                   GROUP BY b.bucket_ts
                   ORDER BY b.bucket_ts
                   """;

        return await ctx.Database
            .SqlQueryRaw<OverviewUsersSeriesBucketRow>(
                sql,
                BuildFilterParams(fromUtc, toUtc, fromUtc.Offset, vpnServerId, externalId))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<OverviewTrafficTotalsRow>> QueryTrafficTotalsPostgresAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var table = ResolveTrafficTable(ctx);
        var sql = $"""
                   WITH {BuildFilteredTrafficCte(table, vpnServerId, externalId)},
                   {BuildBaselinesCte(table, vpnServerId, externalId)},
                   with_prev AS (
                       SELECT
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
                   deltas AS (
                       SELECT
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
                       COALESCE(SUM(d_in), 0)::bigint AS "TrafficInBytes",
                       COALESCE(SUM(d_out), 0)::bigint AS "TrafficOutBytes"
                   FROM deltas
                   """;

        var rows = await ctx.Database
            .SqlQueryRaw<OverviewTrafficTotalsRow>(
                sql,
                BuildFilterParams(fromUtc, toUtc, fromUtc.Offset, vpnServerId, externalId))
            .ToListAsync(ct);

        return rows;
    }

    private async Task<IReadOnlyList<OverviewUserTrafficRow>> QueryUserTrafficRowsPostgresAsync(
        ApplicationDbContext ctx,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId,
        CancellationToken ct)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var table = ResolveTrafficTable(ctx);
        var sql = $"""
                   WITH {BuildFilteredTrafficCte(table, vpnServerId, externalId)},
                   with_prev AS (
                       SELECT
                           f."ExternalId",
                           f."VpnServerId",
                           f."SessionId",
                           f."MeasuredAt",
                           CASE
                               WHEN LAG(f."BytesReceived") OVER w IS NULL THEN 0::bigint
                               WHEN f."BytesReceived" >= LAG(f."BytesReceived") OVER w
                                   THEN f."BytesReceived" - LAG(f."BytesReceived") OVER w
                               ELSE f."BytesReceived"
                           END AS d_in,
                           CASE
                               WHEN LAG(f."BytesSent") OVER w IS NULL THEN 0::bigint
                               WHEN f."BytesSent" >= LAG(f."BytesSent") OVER w
                                   THEN f."BytesSent" - LAG(f."BytesSent") OVER w
                               ELSE f."BytesSent"
                           END AS d_out
                       FROM filtered f
                       WINDOW w AS (PARTITION BY f."SessionId" ORDER BY f."MeasuredAt")
                   )
                   SELECT
                       wp."ExternalId" AS "ExternalId",
                       CASE WHEN COUNT(DISTINCT wp."VpnServerId") = 1 THEN MIN(wp."VpnServerId") END AS "VpnServerId",
                       COUNT(DISTINCT wp."SessionId")::int AS "Sessions",
                       COALESCE(SUM(wp.d_in), 0)::bigint AS "TrafficInBytes",
                       COALESCE(SUM(wp.d_out), 0)::bigint AS "TrafficOutBytes",
                       MIN(wp."MeasuredAt") AS "FirstSeen",
                       MAX(wp."MeasuredAt") AS "LastSeen"
                   FROM with_prev wp
                   GROUP BY wp."ExternalId"
                   ORDER BY COALESCE(SUM(wp.d_in), 0) + COALESCE(SUM(wp.d_out), 0) DESC, wp."ExternalId"
                   """;

        return await ctx.Database
            .SqlQueryRaw<OverviewUserTrafficRow>(
                sql,
                BuildFilterParams(fromUtc, toUtc, fromUtc.Offset, vpnServerId, externalId))
            .ToListAsync(ct);
    }

    /* ---------- in-memory fallback (unit tests) ---------- */

    private IReadOnlyList<OverviewTrafficSample> LoadSamplesInMemory(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var q = uow.GetQuery<VpnServerClientTraffic>().AsQueryable().AsNoTracking();

        if (vpnServerId.HasValue)
            q = q.Where(s => s.VpnServerId == vpnServerId.Value);

        if (!string.IsNullOrWhiteSpace(externalId))
            q = q.Where(s => s.ExternalId == externalId!);

        return q.Where(s => s.MeasuredAt >= fromUtc && s.MeasuredAt < toUtc)
            .Select(s => new OverviewTrafficSample
            {
                VpnServerId = s.VpnServerId,
                SessionId = s.SessionId,
                ExternalId = s.ExternalId ?? "",
                MeasuredAt = s.MeasuredAt,
                BytesIn = s.BytesReceived,
                BytesOut = s.BytesSent
            })
            .OrderBy(s => s.SessionId)
            .ThenBy(s => s.MeasuredAt)
            .ToList();
    }

    private Dictionary<Guid, (long BytesIn, long BytesOut)> LoadBaselinesInMemory(
        DateTimeOffset fromUtc,
        int? vpnServerId,
        string? externalId)
    {
        var trafficQ = uow.GetQuery<VpnServerClientTraffic>().AsQueryable().AsNoTracking();

        if (vpnServerId.HasValue)
            trafficQ = trafficQ.Where(s => s.VpnServerId == vpnServerId.Value);

        if (!string.IsNullOrWhiteSpace(externalId))
            trafficQ = trafficQ.Where(s => s.ExternalId == externalId!);

        var baselineRows = trafficQ
            .Where(s => s.MeasuredAt < fromUtc)
            .GroupBy(s => s.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                LastAt = g.Max(x => x.MeasuredAt)
            })
            .Join(
                trafficQ,
                k => new { k.SessionId, MeasuredAt = k.LastAt },
                s => new { s.SessionId, s.MeasuredAt },
                (k, s) => new
                {
                    s.SessionId,
                    BytesIn = s.BytesReceived,
                    BytesOut = s.BytesSent
                })
            .ToList();

        var dict = new Dictionary<Guid, (long BytesIn, long BytesOut)>(baselineRows.Count);
        foreach (var row in baselineRows)
            dict[row.SessionId] = (row.BytesIn, row.BytesOut);

        return dict;
    }

    private IReadOnlyList<OverviewTrafficBucketRow> AggregateTrafficSeriesBucketsInMemory(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var offset = fromUtc.Offset;
        var mode = ResolveAutoGrouping(fromUtc, toUtc, grouping);
        var samples = LoadSamplesInMemory(fromUtc, toUtc, vpnServerId, externalId);
        var buckets = new Dictionary<DateTimeOffset, (HashSet<Guid> Sessions, long In, long Out)>();
        var lastBySession = new Dictionary<Guid, (long In, long Out)>();

        foreach (var s in samples)
        {
            var bucket = OverviewBucketMath.AlignToBucketStartWithOffset(mode, s.MeasuredAt.ToOffset(TimeSpan.Zero), offset);
            if (!buckets.TryGetValue(bucket, out var acc))
                acc = (new HashSet<Guid>(), 0, 0);

            acc.Sessions.Add(s.SessionId);

            if (lastBySession.TryGetValue(s.SessionId, out var prev))
            {
                var dIn = s.BytesIn >= prev.In ? s.BytesIn - prev.In : s.BytesIn;
                var dOut = s.BytesOut >= prev.Out ? s.BytesOut - prev.Out : s.BytesOut;
                if (dIn < 0) dIn = 0;
                if (dOut < 0) dOut = 0;
                acc.In += dIn;
                acc.Out += dOut;
            }

            lastBySession[s.SessionId] = (s.BytesIn, s.BytesOut);
            buckets[bucket] = acc;
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new OverviewTrafficBucketRow
            {
                BucketTs = kv.Key,
                ActiveClients = kv.Value.Sessions.Count,
                TrafficInBytes = kv.Value.In,
                TrafficOutBytes = kv.Value.Out
            })
            .ToList();
    }

    private IReadOnlyList<OverviewUsersSeriesBucketRow> AggregateUsersSeriesBucketsInMemory(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping,
        int? vpnServerId,
        string? externalId)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var offset = fromUtc.Offset;
        var mode = ResolveAutoGrouping(fromUtc, toUtc, grouping);
        var samples = LoadSamplesInMemory(fromUtc, toUtc, vpnServerId, externalId);
        var buckets = new Dictionary<DateTimeOffset, (HashSet<Guid> Sessions, HashSet<string> Users)>();

        foreach (var s in samples)
        {
            var bucket = OverviewBucketMath.AlignToBucketStartWithOffset(mode, s.MeasuredAt.ToOffset(TimeSpan.Zero), offset);
            if (!buckets.TryGetValue(bucket, out var acc))
                acc = (new HashSet<Guid>(), new HashSet<string>());

            acc.Sessions.Add(s.SessionId);
            if (!string.IsNullOrWhiteSpace(s.ExternalId))
                acc.Users.Add(s.ExternalId);

            buckets[bucket] = acc;
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new OverviewUsersSeriesBucketRow
            {
                BucketTs = kv.Key,
                ActiveSessions = kv.Value.Sessions.Count,
                ActiveUsers = kv.Value.Users.Count
            })
            .ToList();
    }

    private OverviewTrafficTotalsRow AggregateTrafficTotalsInMemory(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var samples = LoadSamplesInMemory(fromUtc, toUtc, vpnServerId, externalId);
        var lastBySession = LoadBaselinesInMemory(fromUtc, vpnServerId, externalId);
        long totalIn = 0;
        long totalOut = 0;

        foreach (var s in samples)
        {
            if (lastBySession.TryGetValue(s.SessionId, out var prev))
            {
                var dIn = s.BytesIn >= prev.BytesIn ? s.BytesIn - prev.BytesIn : s.BytesIn;
                var dOut = s.BytesOut >= prev.BytesOut ? s.BytesOut - prev.BytesOut : s.BytesOut;
                if (dIn < 0) dIn = 0;
                if (dOut < 0) dOut = 0;
                totalIn += dIn;
                totalOut += dOut;
            }

            lastBySession[s.SessionId] = (s.BytesIn, s.BytesOut);
        }

        return new OverviewTrafficTotalsRow { TrafficInBytes = totalIn, TrafficOutBytes = totalOut };
    }

    private IReadOnlyList<OverviewUserTrafficRow> AggregateUserTrafficRowsInMemory(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? vpnServerId,
        string? externalId)
    {
        if (toUtc < fromUtc) (fromUtc, toUtc) = (toUtc, fromUtc);

        var samples = LoadSamplesInMemory(fromUtc, toUtc, vpnServerId, externalId);
        var users = new Dictionary<string, (long In, long Out, HashSet<Guid> Sessions, DateTimeOffset First, DateTimeOffset Last, int? SingleServerId)>();
        var lastBySession = new Dictionary<Guid, (long In, long Out)>();

        foreach (var r in samples)
        {
            if (!users.TryGetValue(r.ExternalId, out var acc))
                acc = (0, 0, new HashSet<Guid>(), r.MeasuredAt, r.MeasuredAt, r.VpnServerId);

            acc.Sessions.Add(r.SessionId);
            if (r.MeasuredAt < acc.First) acc.First = r.MeasuredAt;
            if (r.MeasuredAt > acc.Last) acc.Last = r.MeasuredAt;
            if (acc.SingleServerId.HasValue && acc.SingleServerId.Value != r.VpnServerId)
                acc.SingleServerId = null;

            if (lastBySession.TryGetValue(r.SessionId, out var prev))
            {
                var dIn = r.BytesIn >= prev.In ? r.BytesIn - prev.In : r.BytesIn;
                var dOut = r.BytesOut >= prev.Out ? r.BytesOut - prev.Out : r.BytesOut;
                if (dIn < 0) dIn = 0;
                if (dOut < 0) dOut = 0;
                acc.In += dIn;
                acc.Out += dOut;
            }

            lastBySession[r.SessionId] = (r.BytesIn, r.BytesOut);
            users[r.ExternalId] = acc;
        }

        return users
            .Select(kv => new OverviewUserTrafficRow
            {
                ExternalId = kv.Key,
                VpnServerId = kv.Value.SingleServerId,
                Sessions = kv.Value.Sessions.Count,
                TrafficInBytes = kv.Value.In,
                TrafficOutBytes = kv.Value.Out,
                FirstSeen = kv.Value.First,
                LastSeen = kv.Value.Last
            })
            .OrderByDescending(x => x.TrafficInBytes + x.TrafficOutBytes)
            .ThenBy(x => x.ExternalId)
            .ToList();
    }

    private static OverviewGrouping ResolveAutoGrouping(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        OverviewGrouping grouping)
        => OverviewGroupingRules.ResolveAuto(fromUtc, toUtc, grouping);
}

internal static class OverviewBucketMath
{
    private static DateTimeOffset Shift(DateTimeOffset t, TimeSpan offset) => t.ToOffset(offset);
    private static DateTimeOffset Unshift(DateTimeOffset t) => t.ToOffset(TimeSpan.Zero);

    public static DateTimeOffset AlignToBucketStartWithOffset(OverviewGrouping g, DateTimeOffset tUtc, TimeSpan offset)
    {
        var t = Shift(tUtc, offset);
        var aligned = g switch
        {
            OverviewGrouping.TenMinutes => AlignTenMinuteBucket(t),
            OverviewGrouping.Hours => new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, 0, 0, t.Offset),
            OverviewGrouping.Days => new DateTimeOffset(t.Year, t.Month, t.Day, 0, 0, 0, t.Offset),
            OverviewGrouping.Months => new DateTimeOffset(t.Year, t.Month, 1, 0, 0, 0, t.Offset),
            OverviewGrouping.Years => new DateTimeOffset(t.Year, 1, 1, 0, 0, 0, t.Offset),
            _ => new DateTimeOffset(t.Year, t.Month, t.Day, 0, 0, 0, t.Offset)
        };
        return Unshift(aligned);
    }

    public static DateTimeOffset NextBucketWithOffset(OverviewGrouping g, DateTimeOffset bucketUtc, TimeSpan offset)
    {
        var t = Shift(bucketUtc, offset);
        t = g switch
        {
            OverviewGrouping.TenMinutes => t.AddMinutes(10),
            OverviewGrouping.Hours => t.AddHours(1),
            OverviewGrouping.Days => t.AddDays(1),
            OverviewGrouping.Months => t.AddMonths(1),
            OverviewGrouping.Years => t.AddYears(1),
            _ => t.AddDays(1)
        };
        return Unshift(t);
    }

    private static DateTimeOffset AlignTenMinuteBucket(DateTimeOffset t)
    {
        var minute = t.Minute - (t.Minute % 10);
        return new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, minute, 0, t.Offset);
    }
}
