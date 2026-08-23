using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.Enums;
using FluentAssertions;
using Moq;

namespace DataGateMonitor.Tests.Services.VpnAccess;

public sealed class VpnServerQuotaPlanAccessGuardTests
{
    [Fact]
    public async Task Ensure_WhenUserIsAdmin_SkipsAllowlist()
    {
        const string externalId = "admin-ext";
        const int userId = 7;
        const int vpnServerId = 99;

        var identity = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identity.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = userId, ExternalId = externalId });

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsAdmin = true });

        var plans = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        var allowlist = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var rules = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);

        var sut = new VpnServerQuotaPlanAccessGuard(
            identity.Object,
            plans.Object,
            allowlist.Object,
            rules.Object,
            users.Object);

        await sut.Invoking(s => s.EnsureTargetUserMayUseServerAsync(externalId, vpnServerId, CancellationToken.None))
            .Should().NotThrowAsync();

        plans.Verify(
            q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        allowlist.Verify(
            q => q.GetByQuotaPlanIdAndServerId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        rules.Verify(
            q => q.GetByUserIdAndServerId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ensure_WhenNonAdminPlanDeniesServer_Throws()
    {
        const string externalId = "user-ext";
        const int userId = 8;
        const int planId = 3;
        const int vpnServerId = 55;

        var identity = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identity.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = userId, ExternalId = externalId });

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsAdmin = false });

        var plans = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        plans.Setup(q => q.GetActiveByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { UserId = userId, QuotaPlanId = planId });

        var allowlist = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        allowlist.Setup(q => q.GetByQuotaPlanIdAndServerId(planId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotaPlanAllowedServer?)null);

        var rules = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        rules.Setup(q => q.GetByUserIdAndServerId(userId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVpnServerAccessRule?)null);

        var sut = new VpnServerQuotaPlanAccessGuard(
            identity.Object,
            plans.Object,
            allowlist.Object,
            rules.Object,
            users.Object);

        await sut.Invoking(s => s.EnsureTargetUserMayUseServerAsync(externalId, vpnServerId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(VpnServerAccessErrorKeys.NotAllowedByQuotaPlan);
    }

    [Fact]
    public async Task Ensure_WhenPersonalAllowRuleExists_SkipsPlanAllowlist()
    {
        const string externalId = "granted-ext";
        const int userId = 9;
        const int vpnServerId = 56;

        var identity = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identity.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = userId, ExternalId = externalId });

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsAdmin = false });

        var plans = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        var allowlist = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);

        var rules = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        rules.Setup(q => q.GetByUserIdAndServerId(userId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessRule
            {
                UserId = userId,
                VpnServerId = vpnServerId,
                Mode = VpnServerAccessRuleMode.Allow
            });

        var sut = new VpnServerQuotaPlanAccessGuard(
            identity.Object,
            plans.Object,
            allowlist.Object,
            rules.Object,
            users.Object);

        await sut.Invoking(s => s.EnsureTargetUserMayUseServerAsync(externalId, vpnServerId, CancellationToken.None))
            .Should().NotThrowAsync();

        plans.Verify(
            q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ensure_WhenPersonalDenyRuleExists_Throws_EvenWithoutPlan()
    {
        const string externalId = "blocked-ext";
        const int userId = 10;
        const int vpnServerId = 57;

        var identity = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identity.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = userId, ExternalId = externalId });

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsAdmin = false });

        var plans = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        var allowlist = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);

        var rules = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        rules.Setup(q => q.GetByUserIdAndServerId(userId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessRule
            {
                UserId = userId,
                VpnServerId = vpnServerId,
                Mode = VpnServerAccessRuleMode.Deny
            });

        var sut = new VpnServerQuotaPlanAccessGuard(
            identity.Object,
            plans.Object,
            allowlist.Object,
            rules.Object,
            users.Object);

        await sut.Invoking(s => s.EnsureTargetUserMayUseServerAsync(externalId, vpnServerId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(VpnServerAccessErrorKeys.NotAllowedByQuotaPlan);

        plans.Verify(
            q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ensure_WhenPersonalDeny_AndPlanWouldAllow_Throws()
    {
        const string externalId = "blocked-despite-plan";
        const int userId = 11;
        const int planId = 4;
        const int vpnServerId = 58;

        var identity = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identity.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink { UserId = userId, ExternalId = externalId });

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, IsAdmin = false });

        var plans = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        var allowlist = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);

        var rules = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        rules.Setup(q => q.GetByUserIdAndServerId(userId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessRule
            {
                UserId = userId,
                VpnServerId = vpnServerId,
                Mode = VpnServerAccessRuleMode.Deny
            });

        var sut = new VpnServerQuotaPlanAccessGuard(
            identity.Object,
            plans.Object,
            allowlist.Object,
            rules.Object,
            users.Object);

        await sut.Invoking(s => s.EnsureTargetUserMayUseServerAsync(externalId, vpnServerId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(VpnServerAccessErrorKeys.NotAllowedByQuotaPlan);

        plans.Verify(q => q.GetActiveByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        allowlist.Verify(
            q => q.GetByQuotaPlanIdAndServerId(planId, vpnServerId, It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
