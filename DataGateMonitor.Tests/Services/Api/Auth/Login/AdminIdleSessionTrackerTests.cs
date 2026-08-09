using DataGateMonitor.Services.Api.Auth.Login;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace DataGateMonitor.Tests.Services.Api.Auth.Login;

public class AdminIdleSessionTrackerTests
{
    private static AdminIdleSessionTracker CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AdminIdleTimeoutMinutes"] = "15",
            })
            .Build();
        return new AdminIdleSessionTracker(config, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public void IsExpired_WhenUserIdInvalid_ReturnsTrue()
    {
        Assert.True(CreateSut().IsExpired(0));
    }

    [Fact]
    public void Touch_ThenIsExpired_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.Touch(42);
        Assert.False(sut.IsExpired(42));
    }

    [Fact]
    public void IsAdminRole_MatchesAdminCaseInsensitive()
    {
        Assert.True(AdminIdleSessionTracker.IsAdminRole("admin"));
        Assert.False(AdminIdleSessionTracker.IsAdminRole("user"));
    }
}
