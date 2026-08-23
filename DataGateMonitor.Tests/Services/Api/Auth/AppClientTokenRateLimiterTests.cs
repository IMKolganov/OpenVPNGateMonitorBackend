using Microsoft.Extensions.Caching.Memory;
using DataGateMonitor.Services.Api.Auth;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth;

public class AppClientTokenRateLimiterTests
{
    [Fact]
    public void TryAcquire_Allows_Until_Ip_Limit()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AppClientTokenRateLimiter(cache);

        for (var i = 0; i < 60; i++)
            Assert.True(sut.TryAcquire("203.0.113.1", $"client-{i}"));

        Assert.False(sut.TryAcquire("203.0.113.1", "another"));
    }

    [Fact]
    public void TryAcquire_Limits_By_ClientId_Independently()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AppClientTokenRateLimiter(cache);

        for (var i = 0; i < 30; i++)
            Assert.True(sut.TryAcquire($"203.0.113.{i}", "same-client"));

        Assert.False(sut.TryAcquire("203.0.113.250", "same-client"));
        Assert.True(sut.TryAcquire("203.0.113.250", "other-client"));
    }
}
