using DataGateMonitor.Services.Api.Auth.Login;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace DataGateMonitor.Tests.Services.Api.Auth.Login;

public class AdminIdleSessionTrackerTests
{
    private static AdminIdleSessionTracker CreateSut(int minutes = 15)
    {
        var provider = new Mock<IAdminIdleTimeoutProvider>();
        provider.Setup(p => p.GetMinutes()).Returns(minutes);
        provider.Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(minutes);
        return new AdminIdleSessionTracker(provider.Object, new MemoryCache(new MemoryCacheOptions()));
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
