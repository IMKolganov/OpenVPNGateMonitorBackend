using Npgsql;
using NpgsqlTypes;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// Guards against PostgreSQL 42P08 when optional overview filters are NULL (all servers / all users).
/// Production SQL path is not exercised by in-memory overview query tests.
/// </summary>
public class OverviewTrafficPostgresSqlTests
{
    [Fact]
    public void BuildFilterParams_AllServersNullFilters_TypesNullableParametersExplicitly()
    {
        var from = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(30);

        var parameters = OverviewTrafficPostgresSql.BuildFilterParams(from, to, TimeSpan.FromHours(3), null, null);

        var byName = parameters.ToDictionary(p => p.ParameterName!);

        Assert.Equal(NpgsqlDbType.TimestampTz, byName["from"].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, byName["to"].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, byName["baselineFrom"].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Interval, byName["offset"].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Integer, byName["vpnServerId"].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Text, byName["externalId"].NpgsqlDbType);
        Assert.Equal(DBNull.Value, byName["vpnServerId"].Value);
        Assert.Equal(DBNull.Value, byName["externalId"].Value);
        Assert.Equal(from.UtcDateTime.AddDays(-OverviewTrafficPostgresSql.BaselineLookbackDays), byName["baselineFrom"].Value);
    }

    [Fact]
    public void BuildFilteredTrafficCte_WithoutFilters_UsesMeasuredAtRangeOnly()
    {
        var cte = OverviewTrafficPostgresSql.BuildFilteredTrafficCte(@"""xgb"".""Traffic""");

        Assert.Contains(@"t.""MeasuredAt"" >= @from", cte, StringComparison.Ordinal);
        Assert.Contains(@"t.""MeasuredAt"" < @to", cte, StringComparison.Ordinal);
        Assert.DoesNotContain("@vpnServerId", cte, StringComparison.Ordinal);
        Assert.DoesNotContain("@externalId", cte, StringComparison.Ordinal);
        Assert.DoesNotContain("@offset", cte, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFilteredTrafficCte_WithExternalId_PutsEqualityBeforeTimeRange()
    {
        var cte = OverviewTrafficPostgresSql.BuildFilteredTrafficCte(
            @"""xgb"".""Traffic""", vpnServerId: null, externalId: "user-1");

        var externalIdx = cte.IndexOf(@"t.""ExternalId"" = @externalId", StringComparison.Ordinal);
        var fromIdx = cte.IndexOf(@"t.""MeasuredAt"" >= @from", StringComparison.Ordinal);
        Assert.True(externalIdx >= 0);
        Assert.True(fromIdx > externalIdx);
        Assert.DoesNotContain("IS NULL OR", cte, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBaselinesCte_UsesLookbackWindowNotUnboundedHistory()
    {
        var cte = OverviewTrafficPostgresSql.BuildBaselinesCte(
            @"""xgb"".""Traffic""", vpnServerId: 5, externalId: "user-1");

        Assert.Contains("@baselineFrom", cte, StringComparison.Ordinal);
        Assert.Contains(@"t.""MeasuredAt"" < @from", cte, StringComparison.Ordinal);
        Assert.Contains(@"t.""ExternalId"" = @externalId", cte, StringComparison.Ordinal);
        Assert.Contains(@"t.""VpnServerId"" = @vpnServerId", cte, StringComparison.Ordinal);
        Assert.Contains("DISTINCT ON", cte, StringComparison.Ordinal);
        Assert.Contains("""IN (SELECT DISTINCT f."SessionId" FROM filtered f)""", cte, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeTrafficRange_ClampsFutureTo_AndSwapsInvertedBounds()
    {
        var now = new DateTimeOffset(2026, 8, 9, 15, 0, 0, TimeSpan.Zero);
        var from = new DateTimeOffset(2026, 7, 31, 21, 0, 0, TimeSpan.Zero);
        var toFuture = new DateTimeOffset(2026, 8, 31, 20, 59, 59, TimeSpan.Zero);

        var (nFrom, nTo) = OverviewTrafficPostgresSql.NormalizeTrafficRange(from, toFuture, now);
        Assert.Equal(from, nFrom);
        Assert.Equal(now, nTo);

        var (sFrom, sTo) = OverviewTrafficPostgresSql.NormalizeTrafficRange(toFuture, from, now);
        Assert.Equal(from, sFrom);
        Assert.Equal(now, sTo);
    }

    [Fact]
    public void NormalizeTrafficRange_WhenFromInFuture_CollapsesToEmptyRange()
    {
        var now = new DateTimeOffset(2026, 8, 9, 15, 0, 0, TimeSpan.Zero);
        var from = now.AddDays(1);
        var to = now.AddDays(10);

        var (nFrom, nTo) = OverviewTrafficPostgresSql.NormalizeTrafficRange(from, to, now);
        Assert.Equal(now, nFrom);
        Assert.Equal(now, nTo);
    }

    [Fact]
    public void OverviewQueryTestHelper_UsesInMemoryAggregator_NotPostgresSql()
    {
        // Documents why OpenVpnOverview*QueryTests never caught 42P08: no IDbContextFactory => no raw SQL.
        var uow = new Moq.Mock<DataGateMonitor.DataBase.UnitOfWork.IUnitOfWork>().Object;
        var aggregator = OverviewQueryTestHelper.CreateTrafficAggregator(uow);

        Assert.IsType<OverviewTrafficAggregator>(aggregator);
    }

    [Fact]
    public void BucketStartSql_TenMinutes_UsesFloorOnEpochSeconds()
    {
        var sql = OverviewTrafficPostgresSql.BucketStartSql(OverviewGrouping.TenMinutes);

        Assert.Contains("/ 600", sql, StringComparison.Ordinal);
        Assert.Contains("@offset", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BucketStartSql_Hours_UsesDateTrunc()
    {
        var sql = OverviewTrafficPostgresSql.BucketStartSql(OverviewGrouping.Hours);

        Assert.Contains("date_trunc('hour'", sql, StringComparison.Ordinal);
    }
}
