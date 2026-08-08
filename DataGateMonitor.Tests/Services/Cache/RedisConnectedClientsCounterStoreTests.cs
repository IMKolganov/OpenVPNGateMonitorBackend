using DataGateMonitor.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.Cache;

public class RedisConnectedClientsCounterStoreTests
{
    private static RedisConnectedClientsCounterStore CreateSut(IConfiguration? configuration = null) => new(
        configuration ?? new ConfigurationBuilder().Build(),
        Mock.Of<ILogger<RedisConnectedClientsCounterStore>>());

    [Fact]
    public async Task SetAsync_WhenVpnServerIdInvalid_DoesNotThrow()
    {
        var sut = CreateSut();
        await sut.SetAsync(0, 5, CancellationToken.None);
        await sut.SetAsync(-1, 5, CancellationToken.None);
    }

    [Fact]
    public async Task SetAsync_WhenRedisNotConfigured_IsNoOp()
    {
        var sut = CreateSut();
        await sut.SetAsync(7, 3, CancellationToken.None);
        var map = await sut.GetManyAsync([7], CancellationToken.None);
        Assert.Empty(map);
    }

    [Fact]
    public async Task GetManyAsync_WhenEmptyIds_ReturnsEmpty()
    {
        var map = await CreateSut().GetManyAsync([], CancellationToken.None);
        Assert.Empty(map);
    }
}
