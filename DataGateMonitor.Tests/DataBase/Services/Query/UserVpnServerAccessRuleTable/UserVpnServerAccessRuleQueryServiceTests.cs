using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Enums;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.DataBase.Services.Query.UserVpnServerAccessRuleTable;

public class UserVpnServerAccessRuleQueryServiceTests
{
    [Fact]
    public async Task GetOverridesByUserId_WhenNoRules_ReturnsNone()
    {
        var q = new Mock<IQueryService<UserVpnServerAccessRule, int>>(MockBehavior.Strict);
        q.Setup(x => x.Where(
                It.IsAny<Expression<Func<UserVpnServerAccessRule, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<UserVpnServerAccessRule, object>>[]>()))
            .ReturnsAsync([]);

        var sut = new UserVpnServerAccessRuleQueryService(q.Object);
        var result = await sut.GetOverridesByUserId(42, CancellationToken.None);

        Assert.True(result.IsEmpty);
        Assert.Empty(result.AllowedVpnServerIds);
        Assert.Empty(result.DeniedVpnServerIds);
    }

    [Fact]
    public async Task GetOverridesByUserId_SplitsAllowAndDeny()
    {
        var q = new Mock<IQueryService<UserVpnServerAccessRule, int>>();
        q.Setup(x => x.Where(
                It.IsAny<Expression<Func<UserVpnServerAccessRule, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<UserVpnServerAccessRule, object>>[]>()))
            .ReturnsAsync(
            [
                new UserVpnServerAccessRule { UserId = 1, VpnServerId = 10, Mode = VpnServerAccessRuleMode.Allow },
                new UserVpnServerAccessRule { UserId = 1, VpnServerId = 20, Mode = VpnServerAccessRuleMode.Deny },
                new UserVpnServerAccessRule { UserId = 1, VpnServerId = 30, Mode = VpnServerAccessRuleMode.Allow }
            ]);

        var sut = new UserVpnServerAccessRuleQueryService(q.Object);
        var result = await sut.GetOverridesByUserId(1, CancellationToken.None);

        Assert.Equal([10, 30], result.AllowedVpnServerIds.Order());
        Assert.Equal([20], result.DeniedVpnServerIds.Order());
        Assert.False(result.IsEmpty);
    }
}
