using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerClientTable;

public class OverviewTrafficResultCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_SingleFlights_And_Reuses_Within_Ttl()
    {
        var key = $"test-{Guid.NewGuid():N}";
        var calls = 0;

        async Task<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(30, ct);
            return 42;
        }

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => OverviewTrafficResultCache.GetOrCreateAsync(key, Factory, CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(42, r));
        Assert.Equal(1, calls);

        var again = await OverviewTrafficResultCache.GetOrCreateAsync(key, Factory, CancellationToken.None);
        Assert.Equal(42, again);
        Assert.Equal(1, calls);
    }
}
