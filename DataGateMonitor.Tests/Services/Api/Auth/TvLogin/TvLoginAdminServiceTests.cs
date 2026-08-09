using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.TvLogin;

namespace DataGateMonitor.Tests.Services.Api.Auth.TvLogin;

public class TvLoginAdminServiceTests
{
    [Theory]
    [InlineData(TvLoginSessionStatus.Pending, "pending")]
    [InlineData(TvLoginSessionStatus.Viewed, "viewed")]
    [InlineData(TvLoginSessionStatus.Approved, "approved")]
    [InlineData(TvLoginSessionStatus.Denied, "denied")]
    [InlineData(TvLoginSessionStatus.Expired, "expired")]
    [InlineData(TvLoginSessionStatus.Consumed, "consumed")]
    public void MapStatus_MapsKnownStatuses(TvLoginSessionStatus status, string expected)
    {
        var session = new TvLoginSession
        {
            Status = status,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        };

        Assert.Equal(expected, TvLoginAdminService.MapStatus(session, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MapStatus_PendingPastExpiry_ReturnsExpired()
    {
        var session = new TvLoginSession
        {
            Status = TvLoginSessionStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        Assert.Equal("expired", TvLoginAdminService.MapStatus(session, DateTimeOffset.UtcNow));
    }
}
