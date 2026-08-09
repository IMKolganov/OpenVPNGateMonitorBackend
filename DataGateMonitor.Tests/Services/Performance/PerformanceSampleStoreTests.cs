using DataGateMonitor.Services.Performance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataGateMonitor.Tests.Services.Performance;

public class PerformanceSampleStoreTests
{
    private static PerformanceSampleStore CreateStore(int maxEntries = 5) =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PerformanceMonitoring:MaxEntries"] = maxEntries.ToString()
            }).Build(),
            Options.Create(new PerformanceMonitoringOptions { MaxEntries = maxEntries }),
            NullLogger<PerformanceSampleStore>.Instance);

    [Fact]
    public async Task AppendHttp_ThenGetLatest_ReturnsNewestFirst_AndTrims()
    {
        var store = CreateStore(maxEntries: 3);

        for (var i = 1; i <= 5; i++)
        {
            await store.AppendHttpAsync(new PerformanceHttpSample
            {
                RequestId = $"r{i}",
                Method = "GET",
                Path = $"/api/x/{i}",
                StatusCode = 200,
                DurationMs = 100 + i
            });
        }

        var latest = await store.GetHttpLatestAsync(10);
        Assert.Equal(3, latest.Count);
        Assert.Equal("r5", latest[0].RequestId);
        Assert.Equal("r4", latest[1].RequestId);
        Assert.Equal("r3", latest[2].RequestId);
    }

    [Fact]
    public async Task GetLatest_Clamps_Limit_To_MaxEntries()
    {
        var store = CreateStore(maxEntries: 2);
        await store.AppendHttpAsync(new PerformanceHttpSample
        {
            RequestId = "a",
            Method = "GET",
            Path = "/api/a",
            StatusCode = 200,
            DurationMs = 300
        });
        await store.AppendHttpAsync(new PerformanceHttpSample
        {
            RequestId = "b",
            Method = "GET",
            Path = "/api/b",
            StatusCode = 200,
            DurationMs = 300
        });

        var latest = await store.GetHttpLatestAsync(10_000);
        Assert.Equal(2, latest.Count);
    }

    [Fact]
    public async Task ClearHttp_Leaves_Db_Samples()
    {
        var store = CreateStore();

        await store.AppendHttpAsync(new PerformanceHttpSample
        {
            RequestId = "h1",
            Method = "GET",
            Path = "/api/a",
            StatusCode = 200,
            DurationMs = 300
        });
        await store.AppendDbAsync(new PerformanceDbSample
        {
            RequestId = "h1",
            DurationMs = 150,
            CommandType = "Text",
            Sql = "SELECT 1",
            Succeeded = true
        });

        await store.ClearHttpAsync();
        Assert.Empty(await store.GetHttpLatestAsync(10));
        Assert.Single(await store.GetDbLatestAsync(10));
    }

    [Fact]
    public async Task AppendDb_AndClear_WorksIndependentlyOfHttp()
    {
        var store = CreateStore();

        await store.AppendHttpAsync(new PerformanceHttpSample
        {
            RequestId = "h1",
            Method = "GET",
            Path = "/api/a",
            StatusCode = 200,
            DurationMs = 300
        });
        await store.AppendDbAsync(new PerformanceDbSample
        {
            RequestId = "h1",
            DurationMs = 150,
            CommandType = "Text",
            Sql = "SELECT 1",
            Succeeded = true
        });

        Assert.Single(await store.GetHttpLatestAsync(10));
        Assert.Single(await store.GetDbLatestAsync(10));

        await store.ClearDbAsync();
        Assert.Single(await store.GetHttpLatestAsync(10));
        Assert.Empty(await store.GetDbLatestAsync(10));

        await store.ClearAllAsync();
        Assert.Empty(await store.GetHttpLatestAsync(10));
    }

    [Fact]
    public async Task Concurrent_Appends_Do_Not_Throw_And_Respect_Cap()
    {
        var store = CreateStore(maxEntries: 20);
        var tasks = Enumerable.Range(0, 100).Select(i =>
            store.AppendHttpAsync(new PerformanceHttpSample
            {
                RequestId = $"c{i}",
                Method = "GET",
                Path = $"/api/c/{i}",
                StatusCode = 200,
                DurationMs = 200 + i
            }));

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);

        var latest = await store.GetHttpLatestAsync(100);
        Assert.Equal(20, latest.Count);
    }
}
