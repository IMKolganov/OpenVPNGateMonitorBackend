using System.IdentityModel.Tokens.Jwt;
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

public class TokenServiceLocalIdentityTests
{
    private const string JwtSecret = "VeryStrongTestSecretKey1234567890";
    private const string JwtPepper = "test-pepper-at-least-32-chars-long";

    private static TokenService CreateSut(
        User user,
        IReadOnlyList<UserIdentityLink> links,
        out Mock<ICommandService<UserRefreshToken, int>> refreshTokenCommand)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["Jwt:RefreshPepper"] = JwtPepper,
                ["Jwt:LifetimeMinutes"] = "15",
                ["Jwt:RefreshLifetimeDays"] = "30",
            })
            .Build();

        var userQuery = new Mock<IUserQueryService>();
        userQuery.Setup(q => q.GetById(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var userRoleService = new Mock<IUserRoleService>();
        userRoleService
            .Setup(s => s.GetUserRoleNameAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("VpnUser");

        refreshTokenCommand = new Mock<ICommandService<UserRefreshToken, int>>();
        refreshTokenCommand
            .Setup(c => c.Add(It.IsAny<UserRefreshToken>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRefreshToken t, bool _, CancellationToken _) => t);

        var userIdentityLinkQuery = new Mock<IUserIdentityLinkQueryService>();
        userIdentityLinkQuery
            .Setup(q => q.GetListByUserId(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(links.ToList());

        var idleTimeout = new Mock<IAdminIdleTimeoutProvider>();
        idleTimeout.Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(15);

        return new TokenService(
            config,
            userQuery.Object,
            userRoleService.Object,
            new Mock<IUserRefreshTokenQueryService>().Object,
            refreshTokenCommand.Object,
            userIdentityLinkQuery.Object,
            new Mock<IAdminIdleSessionTracker>().Object,
            idleTimeout.Object);
    }

    private static string ReadExternalIdClaim(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.First(c => c.Type == "externalId").Value;
    }

    [Fact]
    public async Task IssueAsync_WhenOnlyLocalLink_PutsPrefixedExternalIdInJwt()
    {
        var user = new User { Id = 42, DisplayName = "Email User", IsBlocked = false };
        var links = new List<UserIdentityLink>
        {
            new()
            {
                UserId = 42,
                Provider = AuthIdentityProviders.Local,
                ExternalId = "local:42",
            },
        };

        var sut = CreateSut(user, links, out _);

        var tokenPair = await sut.IssueAsync(42, externalId: null, null, null, CancellationToken.None);

        Assert.Equal("local:42", ReadExternalIdClaim(tokenPair.AccessToken));
    }

    [Fact]
    public async Task IssueAsync_WhenGoogleSubPassedExplicitly_KeepsGoogleSubEvenIfLocalLinkExists()
    {
        var user = new User { Id = 10, DisplayName = "Hybrid User", IsBlocked = false };
        var links = new List<UserIdentityLink>
        {
            new() { UserId = 10, Provider = AuthIdentityProviders.Local, ExternalId = "local:10" },
            new() { UserId = 10, Provider = AuthIdentityProviders.Google, ExternalId = "google-sub-abc" },
        };

        var sut = CreateSut(user, links, out _);

        var tokenPair = await sut.IssueAsync(10, "google-sub-abc", null, null, CancellationToken.None);

        Assert.Equal("google-sub-abc", ReadExternalIdClaim(tokenPair.AccessToken));
    }

    [Fact]
    public async Task IssueAsync_WhenLocalAndGoogleLinksExistWithoutExplicitId_PrefersGoogle()
    {
        var user = new User { Id = 10, DisplayName = "Hybrid User", IsBlocked = false };
        var links = new List<UserIdentityLink>
        {
            new() { UserId = 10, Provider = AuthIdentityProviders.Local, ExternalId = "local:10" },
            new() { UserId = 10, Provider = AuthIdentityProviders.Google, ExternalId = "google-sub-abc" },
        };

        var sut = CreateSut(user, links, out _);

        var tokenPair = await sut.IssueAsync(10, externalId: null, null, null, CancellationToken.None);

        Assert.Equal("google-sub-abc", ReadExternalIdClaim(tokenPair.AccessToken));
    }

    [Fact]
    public async Task IssueAsync_WhenTelegramAndLocalLinksExist_PrefersTelegram()
    {
        var user = new User { Id = 7, DisplayName = "Tg+Password", IsBlocked = false };
        var links = new List<UserIdentityLink>
        {
            new() { UserId = 7, Provider = AuthIdentityProviders.Local, ExternalId = "local:7" },
            new() { UserId = 7, Provider = AuthIdentityProviders.Telegram, ExternalId = "555001" },
        };

        var sut = CreateSut(user, links, out _);

        var tokenPair = await sut.IssueAsync(7, externalId: null, null, null, CancellationToken.None);

        Assert.Equal("555001", ReadExternalIdClaim(tokenPair.AccessToken));
    }
}
