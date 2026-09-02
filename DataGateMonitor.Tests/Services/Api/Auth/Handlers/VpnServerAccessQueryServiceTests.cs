using Moq;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Handlers;
using DataGateMonitor.SharedModels.Enums;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth.Handlers;

/// <summary>
/// Legacy Android compatibility regression tests for quota/access fallback behavior,
/// plus precedence of per-user access rules over the quota plan.
/// </summary>
public class VpnServerAccessQueryServiceTests
{
    private readonly Mock<IUserQuotaPlanQueryService> _quotaQuery = new();
    private readonly Mock<IQuotaPlanAllowedServerQueryService> _allowedQuery = new();
    private readonly Mock<IUserVpnServerAccessRuleQueryService> _ruleQuery = new();

    public VpnServerAccessQueryServiceTests()
    {
        _ruleQuery
            .Setup(q => q.GetByUserIdAndServerId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVpnServerAccessRule?)null);
    }

    private VpnServerAccessQueryService CreateSut() =>
        new(_quotaQuery.Object, _allowedQuery.Object, _ruleQuery.Object);

    [Fact]
    public async Task UserHasAccessAsync_When_UserHasQuotaPlan_And_ServerAllowed_Returns_True()
    {
        var userQuotaPlan = new UserQuotaPlan { UserId = 1, QuotaPlanId = 5 };
        var allowed = new QuotaPlanAllowedServer { QuotaPlanId = 5, VpnServerId = 10 };

        _quotaQuery.Setup(q => q.GetActiveByUserId(1, It.IsAny<CancellationToken>())).ReturnsAsync(userQuotaPlan);
        _allowedQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(5, 10, It.IsAny<CancellationToken>())).ReturnsAsync(allowed);

        var result = await CreateSut().UserHasAccessAsync(1, 10, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task UserHasAccessAsync_When_UserHasNoQuotaPlan_Returns_True()
    {
        _quotaQuery.Setup(q => q.GetActiveByUserId(1, It.IsAny<CancellationToken>())).ReturnsAsync((UserQuotaPlan?)null);

        var result = await CreateSut().UserHasAccessAsync(1, 10, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task UserHasAccessAsync_When_ServerNotAllowed_Returns_False()
    {
        var userQuotaPlan = new UserQuotaPlan { UserId = 1, QuotaPlanId = 5 };
        _quotaQuery.Setup(q => q.GetActiveByUserId(1, It.IsAny<CancellationToken>())).ReturnsAsync(userQuotaPlan);
        _allowedQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(5, 10, It.IsAny<CancellationToken>())).ReturnsAsync((QuotaPlanAllowedServer?)null);

        var result = await CreateSut().UserHasAccessAsync(1, 10, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task UserHasAccessAsync_When_PersonalAllowRule_ShortCircuitsBeforePlan()
    {
        _ruleQuery.Setup(q => q.GetByUserIdAndServerId(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessRule { UserId = 1, VpnServerId = 10, Mode = VpnServerAccessRuleMode.Allow });

        var result = await CreateSut().UserHasAccessAsync(1, 10, CancellationToken.None);

        Assert.True(result);
        _quotaQuery.Verify(q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _allowedQuery.Verify(
            q => q.GetByQuotaPlanIdAndServerId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UserHasAccessAsync_When_PersonalDenyRule_ShortCircuitsEvenIfPlanWouldAllow()
    {
        _ruleQuery.Setup(q => q.GetByUserIdAndServerId(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessRule { UserId = 1, VpnServerId = 10, Mode = VpnServerAccessRuleMode.Deny });
        // Plan would allow — must not be consulted once a personal deny exists.
        _quotaQuery.Setup(q => q.GetActiveByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { UserId = 1, QuotaPlanId = 5 });
        _allowedQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlanAllowedServer { QuotaPlanId = 5, VpnServerId = 10 });

        var result = await CreateSut().UserHasAccessAsync(1, 10, CancellationToken.None);

        Assert.False(result);
        _quotaQuery.Verify(q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _allowedQuery.Verify(
            q => q.GetByQuotaPlanIdAndServerId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
