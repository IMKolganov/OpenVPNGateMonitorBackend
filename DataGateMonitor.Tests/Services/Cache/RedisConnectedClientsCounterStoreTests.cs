using DataGateMonitor.Services.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace DataGateMonitor.Tests.Services.Cache;

/// <summary>Redis optional: store must no-op when the database provider returns null.</summary>
public class RedisConnectedClientsCounterStoreWithoutRedisTests
{
    private static RedisConnectedClientsCounterStore CreateSut(IRedisDatabaseProvider provider) => new(
        provider,
        Mock.Of<ILogger<RedisConnectedClientsCounterStore>>());

    private static IRedisDatabaseProvider UnavailableProvider()
    {
        var provider = new Mock<IRedisDatabaseProvider>(MockBehavior.Strict);
        provider
            .Setup(p => p.GetDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDatabase?)null);
        return provider.Object;
    }

    [Fact]
    public async Task SetAsync_WhenVpnServerIdInvalid_DoesNotTouchRedis()
    {
        var provider = new Mock<IRedisDatabaseProvider>(MockBehavior.Strict);
        var sut = CreateSut(provider.Object);

        await sut.SetAsync(0, 5, CancellationToken.None);
        await sut.SetAsync(-1, 5, CancellationToken.None);

        provider.Verify(p => p.GetDatabaseAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenRedisUnavailable_IsNoOp()
    {
        var sut = CreateSut(UnavailableProvider());
        await sut.SetAsync(7, 3, CancellationToken.None);
        Assert.Empty(await sut.GetManyAsync([7], CancellationToken.None));
    }

    [Fact]
    public async Task GetManyAsync_WhenEmptyIds_ReturnsEmpty_WithoutCallingRedis()
    {
        var provider = new Mock<IRedisDatabaseProvider>(MockBehavior.Strict);
        var map = await CreateSut(provider.Object).GetManyAsync([], CancellationToken.None);
        Assert.Empty(map);
        provider.Verify(p => p.GetDatabaseAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetManyAsync_WhenRedisUnavailable_ReturnsEmpty()
    {
        var map = await CreateSut(UnavailableProvider()).GetManyAsync([7, 8], CancellationToken.None);
        Assert.Empty(map);
    }
}

/// <summary>Redis present: verify key shape, TTL, clamp, and soft-fail on Redis errors.</summary>
public class RedisConnectedClientsCounterStoreWithRedisTests
{
    private static (RedisConnectedClientsCounterStore Sut, Mock<IDatabase> Db) CreateSut()
    {
        var db = new Mock<IDatabase>(MockBehavior.Strict);
        var provider = new Mock<IRedisDatabaseProvider>(MockBehavior.Strict);
        provider
            .Setup(p => p.GetDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(db.Object);

        return (new RedisConnectedClientsCounterStore(
            provider.Object,
            Mock.Of<ILogger<RedisConnectedClientsCounterStore>>()), db);
    }

    private static void SetupStringSet(Mock<IDatabase> db, Action<RedisKey, RedisValue, TimeSpan?>? capture = null)
    {
        db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, expiry, _, _) =>
                capture?.Invoke(key, value, expiry))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task SetAsync_WritesNonNegativeCount_WithPrefixKeyAndTtl()
    {
        var (sut, db) = CreateSut();
        RedisKey? capturedKey = null;
        RedisValue capturedValue = default;
        TimeSpan? capturedTtl = null;
        SetupStringSet(db, (key, value, expiry) =>
        {
            capturedKey = key;
            capturedValue = value;
            capturedTtl = expiry;
        });

        await sut.SetAsync(7, 3, CancellationToken.None);

        Assert.Equal($"{RedisConnectedClientsCounterStore.KeyPrefix}7", capturedKey.ToString());
        Assert.Equal("3", capturedValue.ToString());
        Assert.Equal(RedisConnectedClientsCounterStore.CounterTtl, capturedTtl);
    }

    [Fact]
    public async Task SetAsync_ClampsNegativeCountToZero()
    {
        var (sut, db) = CreateSut();
        RedisValue capturedValue = default;
        SetupStringSet(db, (_, value, _) => capturedValue = value);

        await sut.SetAsync(7, -5, CancellationToken.None);

        Assert.Equal("0", capturedValue.ToString());
    }

    [Fact]
    public async Task SetAsync_WhenWriteThrows_DoesNotPropagate()
    {
        var (sut, db) = CreateSut();
        db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        await sut.SetAsync(7, 1, CancellationToken.None);
    }

    [Fact]
    public async Task GetManyAsync_ReturnsParsedCounts_SkipsMissingAndGarbage()
    {
        var (sut, db) = CreateSut();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "3", RedisValue.Null, "bad", "-2" });

        var map = await sut.GetManyAsync([7, 8, 9, 10], CancellationToken.None);

        Assert.Equal(2, map.Count);
        Assert.Equal(3, map[7]);
        Assert.Equal(0, map[10]);
        Assert.False(map.ContainsKey(8));
        Assert.False(map.ContainsKey(9));
    }

    [Fact]
    public async Task GetManyAsync_WhenReadThrows_ReturnsEmpty()
    {
        var (sut, db) = CreateSut();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        Assert.Empty(await sut.GetManyAsync([7], CancellationToken.None));
    }
}

public class ConfigurationRedisDatabaseProviderTests
{
    private static IConfiguration ConfigWithRedis(string? connectionString)
    {
        var dict = new Dictionary<string, string?>();
        if (connectionString is not null)
            dict["ConnectionStrings:Redis"] = connectionString;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task GetDatabaseAsync_WhenNotConfigured_ReturnsNull_WithoutConnecting()
    {
        var connector = new Mock<IRedisMultiplexerConnector>(MockBehavior.Strict);
        var sut = new ConfigurationRedisDatabaseProvider(
            ConfigWithRedis(null),
            Mock.Of<ILogger<ConfigurationRedisDatabaseProvider>>(),
            connector.Object);

        Assert.Null(await sut.GetDatabaseAsync(CancellationToken.None));
        connector.Verify(
            c => c.ConnectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDatabaseAsync_WhenConnectFails_ReturnsNull_AndDoesNotRetry()
    {
        var calls = 0;
        var connector = new Mock<IRedisMultiplexerConnector>(MockBehavior.Strict);
        connector
            .Setup(c => c.ConnectAsync("localhost:6379", It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                return Task.FromException<IConnectionMultiplexer>(
                    new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
            });

        var sut = new ConfigurationRedisDatabaseProvider(
            ConfigWithRedis("localhost:6379"),
            Mock.Of<ILogger<ConfigurationRedisDatabaseProvider>>(),
            connector.Object);

        Assert.Null(await sut.GetDatabaseAsync(CancellationToken.None));
        Assert.Null(await sut.GetDatabaseAsync(CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetDatabaseAsync_WhenConnected_ReturnsDatabase()
    {
        var db = Mock.Of<IDatabase>();
        var mux = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        mux.SetupGet(m => m.IsConnected).Returns(true);
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db);

        var connector = new Mock<IRedisMultiplexerConnector>(MockBehavior.Strict);
        connector
            .Setup(c => c.ConnectAsync("localhost:6379", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mux.Object);

        var sut = new ConfigurationRedisDatabaseProvider(
            ConfigWithRedis("localhost:6379"),
            Mock.Of<ILogger<ConfigurationRedisDatabaseProvider>>(),
            connector.Object);

        Assert.Same(db, await sut.GetDatabaseAsync(CancellationToken.None));
        Assert.Same(db, await sut.GetDatabaseAsync(CancellationToken.None));
        connector.Verify(c => c.ConnectAsync("localhost:6379", It.IsAny<CancellationToken>()), Times.Once);
    }
}
