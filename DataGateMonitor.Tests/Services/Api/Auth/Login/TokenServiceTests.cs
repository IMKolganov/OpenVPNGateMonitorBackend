using Microsoft.Extensions.Configuration;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserRefreshTokenTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth.Login;

public class TokenServiceTests
{
    [Fact]
    public async Task IssueAsync_When_UserNotFound_ThrowsInvalidOperationException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection("Jwt")["RefreshLifetimeDays"]).Returns((string?)null);
        var userQuery = new Mock<IUserQueryService>();
        userQuery.Setup(q => q.GetById(999, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var userRoleService = new Mock<IUserRoleService>();
        var refreshTokenQuery = new Mock<IUserRefreshTokenQueryService>();
        var refreshTokenCommand = new Mock<ICommandService<UserRefreshToken, int>>();
        var userIdentityLinkQuery = new Mock<IUserIdentityLinkQueryService>();
        var adminIdleTracker = new Mock<IAdminIdleSessionTracker>();
        var adminIdleTimeout = new Mock<IAdminIdleTimeoutProvider>();
        adminIdleTimeout.Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15);

        var sut = new TokenService(
            config.Object,
            userQuery.Object,
            userRoleService.Object,
            refreshTokenQuery.Object,
            refreshTokenCommand.Object,
            userIdentityLinkQuery.Object,
            adminIdleTracker.Object,
            adminIdleTimeout.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.IssueAsync(999, null, null, null, CancellationToken.None));

        Assert.Equal("User not found.", ex.Message);
        refreshTokenCommand.Verify(
            c => c.Add(It.IsAny<UserRefreshToken>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IssueAsync_When_UserBlocked_ThrowsUnauthorizedAccessException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection("Jwt")["RefreshLifetimeDays"]).Returns((string?)null);
        var user = new User { Id = 1, DisplayName = "U", IsBlocked = true };
        var userQuery = new Mock<IUserQueryService>();
        userQuery.Setup(q => q.GetById(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var userRoleService = new Mock<IUserRoleService>();
        var refreshTokenQuery = new Mock<IUserRefreshTokenQueryService>();
        var refreshTokenCommand = new Mock<ICommandService<UserRefreshToken, int>>();
        var userIdentityLinkQuery = new Mock<IUserIdentityLinkQueryService>();
        var adminIdleTracker = new Mock<IAdminIdleSessionTracker>();
        var adminIdleTimeout = new Mock<IAdminIdleTimeoutProvider>();
        adminIdleTimeout.Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15);

        var sut = new TokenService(
            config.Object,
            userQuery.Object,
            userRoleService.Object,
            refreshTokenQuery.Object,
            refreshTokenCommand.Object,
            userIdentityLinkQuery.Object,
            adminIdleTracker.Object,
            adminIdleTimeout.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.IssueAsync(1, null, null, null, CancellationToken.None));

        Assert.Equal("User account is blocked.", ex.Message);
        refreshTokenCommand.Verify(
            c => c.Add(It.IsAny<UserRefreshToken>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
