using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserRefreshTokenTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.Auth;
using Microsoft.Extensions.Configuration;
using Moq;

namespace DataGateMonitor.Tests.Services.Api.Auth.Login;

/// <summary>
/// Session policy by role: Admin idle is enforced on refresh; VpnUser (e.g. Google app clients) is not.
/// </summary>
public class TokenServiceIdleAndRoleTests
{
    private const string JwtSecret = "VeryStrongTestSecretKey1234567890";
    private const string JwtPepper = "test-pepper-at-least-32-chars-long";

    private sealed class SutBundle
    {
        public required TokenService Sut { get; init; }
        public required Mock<IAdminIdleSessionTracker> IdleTracker { get; init; }
        public required Mock<IUserRefreshTokenQueryService> RefreshQuery { get; init; }
        public required Mock<ICommandService<UserRefreshToken, int>> RefreshCommand { get; init; }
        public required Mock<IUserRoleService> RoleService { get; init; }
    }

    private static SutBundle CreateBundle(User user, string roleName)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["Jwt:RefreshPepper"] = JwtPepper,
                ["Jwt:LifetimeMinutes"] = "60",
                ["Jwt:RefreshLifetimeDays"] = "30",
                ["Jwt:Issuer"] = "OpenVPNGateBackend",
                ["Jwt:Audience"] = "OpenVPNGateFrontend",
            })
            .Build();

        var userQuery = new Mock<IUserQueryService>();
        userQuery.Setup(q => q.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var roleService = new Mock<IUserRoleService>();
        roleService
            .Setup(s => s.GetUserRoleNameAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleName);

        var refreshQuery = new Mock<IUserRefreshTokenQueryService>();
        var refreshCommand = new Mock<ICommandService<UserRefreshToken, int>>();
        var nextId = 1;
        refreshCommand
            .Setup(c => c.Add(It.IsAny<UserRefreshToken>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRefreshToken t, bool _, CancellationToken _) =>
            {
                t.Id = nextId++;
                return t;
            });
        refreshCommand
            .Setup(c => c.Update(It.IsAny<UserRefreshToken>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var identityLinks = new Mock<IUserIdentityLinkQueryService>();
        identityLinks
            .Setup(q => q.GetListByUserId(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserIdentityLink>());

        var idleTracker = new Mock<IAdminIdleSessionTracker>();
        var idleTimeout = new Mock<IAdminIdleTimeoutProvider>();
        idleTimeout.Setup(p => p.GetMinutes()).Returns(15);
        idleTimeout.Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15);

        var sut = new TokenService(
            config,
            userQuery.Object,
            roleService.Object,
            refreshQuery.Object,
            refreshCommand.Object,
            identityLinks.Object,
            idleTracker.Object,
            idleTimeout.Object);

        return new SutBundle
        {
            Sut = sut,
            IdleTracker = idleTracker,
            RefreshQuery = refreshQuery,
            RefreshCommand = refreshCommand,
            RoleService = roleService,
        };
    }

    private static void StubValidRefreshToken(SutBundle bundle, int userId, string rawRefresh = "client-refresh-token")
    {
        bundle.RefreshQuery
            .Setup(q => q.GetByTokenHash(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRefreshToken
            {
                Id = 100,
                UserId = userId,
                TokenHash = "ignored-hash",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(20),
                RevokedAt = null,
            });
    }

    private static bool HasIdleClaim(string accessToken) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken)
            .Claims
            .Any(c => c.Type == "adminIdleTimeoutMinutes");

    private static string ReadRoleClaim(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.First(c =>
            c.Type == ClaimTypes.Role || c.Type == "role").Value;
    }

    [Fact]
    public async Task IssueAsync_Admin_TouchesIdleAndAddsIdleClaim()
    {
        var user = new User { Id = 1, DisplayName = "Admin", IsBlocked = false };
        var bundle = CreateBundle(user, SystemRoles.AdminName);

        var pair = await bundle.Sut.IssueAsync(1, "admin-ext", null, "ua", CancellationToken.None);

        Assert.Equal(SystemRoles.AdminName, ReadRoleClaim(pair.AccessToken));
        Assert.True(HasIdleClaim(pair.AccessToken));
        bundle.IdleTracker.Verify(t => t.Touch(1), Times.Once);
        bundle.IdleTracker.Verify(t => t.IsExpired(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_VpnUser_DoesNotTouchIdleOrAddIdleClaim()
    {
        var user = new User { Id = 9, DisplayName = "Google Client", Email = "u@g.com", IsBlocked = false };
        var bundle = CreateBundle(user, SystemRoles.VpnUserName);

        var pair = await bundle.Sut.IssueAsync(9, "google-sub-xyz", null, "DataGateVPN/Android", CancellationToken.None);

        Assert.Equal(SystemRoles.VpnUserName, ReadRoleClaim(pair.AccessToken));
        Assert.False(HasIdleClaim(pair.AccessToken));
        bundle.IdleTracker.Verify(t => t.Touch(It.IsAny<int>()), Times.Never);
        bundle.IdleTracker.Verify(t => t.IsExpired(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_Admin_WhenIdleExpired_ThrowsUnauthorized()
    {
        var user = new User { Id = 2, DisplayName = "Admin", IsBlocked = false };
        var bundle = CreateBundle(user, SystemRoles.AdminName);
        StubValidRefreshToken(bundle, user.Id);
        bundle.IdleTracker.Setup(t => t.IsExpired(2)).Returns(true);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => bundle.Sut.RefreshAsync("client-refresh-token", null, "ua", CancellationToken.None));

        Assert.Contains("inactivity", ex.Message, StringComparison.OrdinalIgnoreCase);
        bundle.IdleTracker.Verify(t => t.IsExpired(2), Times.Once);
        bundle.IdleTracker.Verify(t => t.Touch(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_Admin_WhenIdleActive_TouchesAndIssuesTokens()
    {
        var user = new User { Id = 3, DisplayName = "Admin", IsBlocked = false };
        var bundle = CreateBundle(user, SystemRoles.AdminName);
        StubValidRefreshToken(bundle, user.Id);
        bundle.IdleTracker.Setup(t => t.IsExpired(3)).Returns(false);

        var pair = await bundle.Sut.RefreshAsync("client-refresh-token", null, "ua", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(pair.AccessToken));
        Assert.True(HasIdleClaim(pair.AccessToken));
        bundle.IdleTracker.Verify(t => t.IsExpired(3), Times.Once);
        bundle.IdleTracker.Verify(t => t.Touch(3), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_VpnUser_SkipsIdleCheckEvenIfTrackerWouldExpire()
    {
        var user = new User { Id = 11, DisplayName = "App User", IsBlocked = false };
        var bundle = CreateBundle(user, SystemRoles.VpnUserName);
        StubValidRefreshToken(bundle, user.Id);
        // If idle were incorrectly applied, this would fail the refresh.
        bundle.IdleTracker.Setup(t => t.IsExpired(11)).Returns(true);

        var pair = await bundle.Sut.RefreshAsync("client-refresh-token", null, "DataGateVPN/iOS", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(pair.AccessToken));
        Assert.Equal(SystemRoles.VpnUserName, ReadRoleClaim(pair.AccessToken));
        Assert.False(HasIdleClaim(pair.AccessToken));
        bundle.IdleTracker.Verify(t => t.IsExpired(It.IsAny<int>()), Times.Never);
        bundle.IdleTracker.Verify(t => t.Touch(It.IsAny<int>()), Times.Never);
    }
}
