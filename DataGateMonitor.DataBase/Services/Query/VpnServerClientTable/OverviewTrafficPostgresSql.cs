using Npgsql;
using NpgsqlTypes;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// PostgreSQL text and parameters for overview traffic aggregation.
/// Extracted so parameter typing is unit-tested (42P08 when nullable filters are untyped).
/// </summary>
public static class OverviewTrafficPostgresSql
{
    /// <summary>
    /// How far before <c>@from</c> to search for per-session byte baselines.
    /// Polling is frequent; 2 days covers overnight sessions without scanning full history
    /// (heavy ExternalId users can have 100k+ rows).
    /// </summary>
    public const int BaselineLookbackDays = 2;

    public const string VpnServerFilterSql = """(@vpnServerId IS NULL OR t."VpnServerId" = @vpnServerId)""";
    public const string ExternalIdFilterSql = """(@externalId IS NULL OR t."ExternalId" = @externalId)""";

    /// <summary>
    /// Swap inverted bounds and clamp <paramref name="toUtc"/> to <paramref name="utcNow"/>
    /// (apps often send month-end <c>To</c>; there is no traffic in the future).
    /// </summary>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeTrafficRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset utcNow)
    {
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        // No samples exist in the future — collapse to an empty window at "now".
        if (fromUtc > utcNow)
            return (utcNow, utcNow);

        if (toUtc > utcNow)
            toUtc = utcNow;

        return (fromUtc, toUtc);
    }

    public static NpgsqlParameter[] BuildFilterParams(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        TimeSpan offset,
        int? vpnServerId,
        string? externalId)
    {
        var baselineFrom = fromUtc.UtcDateTime.AddDays(-BaselineLookbackDays);
        return
        [
            new("from", NpgsqlDbType.TimestampTz) { Value = fromUtc.UtcDateTime },
            new("to", NpgsqlDbType.TimestampTz) { Value = toUtc.UtcDateTime },
            new("baselineFrom", NpgsqlDbType.TimestampTz) { Value = baselineFrom },
            new("offset", NpgsqlDbType.Interval) { Value = offset },
            new("vpnServerId", NpgsqlDbType.Integer) { Value = (object?)vpnServerId ?? DBNull.Value },
            new("externalId", NpgsqlDbType.Text) { Value = (object?)externalId ?? DBNull.Value },
        ];
    }

    public static string BuildFilteredTrafficCte(string table, int? vpnServerId, string? externalId)
    {
        var predicates = BuildEqualityAndRangePredicates(
            vpnServerId,
            externalId,
            measuredFromParam: "@from",
            measuredToExclusiveParam: "@to");

        return $"""
               filtered AS (
                   SELECT
                       t."SessionId",
                       t."ExternalId",
                       t."VpnServerId",
                       t."MeasuredAt",
                       t."BytesReceived",
                       t."BytesSent"
                   FROM {table} t
                   WHERE {predicates}
               )
               """;
    }

    /// <summary>Backward-compatible overload (optional filters as OR-NULL).</summary>
    public static string BuildFilteredTrafficCte(string table)
        => BuildFilteredTrafficCte(table, vpnServerId: null, externalId: null);

    public static string BuildBaselinesCte(string table, int? vpnServerId, string? externalId)
    {
        var predicates = BuildEqualityAndRangePredicates(
            vpnServerId,
            externalId,
            measuredFromParam: "@baselineFrom",
            measuredToExclusiveParam: "@from");

        return $"""
               baselines AS (
                   SELECT DISTINCT ON (t."SessionId")
                       t."SessionId",
                       t."BytesReceived" AS baseline_in,
                       t."BytesSent" AS baseline_out
                   FROM {table} t
                   WHERE {predicates}
                   ORDER BY t."SessionId", t."MeasuredAt" DESC
               )
               """;
    }

    private static string BuildEqualityAndRangePredicates(
        int? vpnServerId,
        string? externalId,
        string measuredFromParam,
        string measuredToExclusiveParam)
    {
        // Equality first when present so the planner prefers IX_ClientTraffic_External_At /
        // server+time indexes instead of a wide MeasuredAt scan + filter.
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(externalId))
            parts.Add("""t."ExternalId" = @externalId""");
        if (vpnServerId.HasValue)
            parts.Add("""t."VpnServerId" = @vpnServerId""");
        parts.Add($"""t."MeasuredAt" >= {measuredFromParam}""");
        parts.Add($"""t."MeasuredAt" < {measuredToExclusiveParam}""");
        return string.Join("\n                     AND ", parts);
    }

    public static string BucketStartSql(OverviewGrouping grouping)
    {
        if (grouping == OverviewGrouping.TenMinutes)
            return TenMinuteBucketStartSql();

        var truncUnit = grouping switch
        {
            OverviewGrouping.Hours => "hour",
            OverviewGrouping.Days => "day",
            OverviewGrouping.Months => "month",
            OverviewGrouping.Years => "year",
            _ => "day",
        };

        return DateTruncBucketStartSql(truncUnit);
    }

    private static string DateTruncBucketStartSql(string truncUnit)
        => $"""((date_trunc('{truncUnit}', ("MeasuredAt" AT TIME ZONE 'UTC') + @offset::interval) AT TIME ZONE 'UTC') - @offset::interval)::timestamptz""";

    private static string TenMinuteBucketStartSql()
        => $"""((to_timestamp(floor(extract(epoch from ("MeasuredAt" AT TIME ZONE 'UTC') + @offset::interval) / {OverviewGroupingRules.TenMinuteBucketSeconds}) * {OverviewGroupingRules.TenMinuteBucketSeconds}) AT TIME ZONE 'UTC') - @offset::interval)::timestamptz""";
}
