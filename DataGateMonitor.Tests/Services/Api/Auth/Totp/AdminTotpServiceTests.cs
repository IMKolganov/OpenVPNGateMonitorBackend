using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth.Totp;

public class AdminTotpServiceTests
{
    private static AdminTotpService CreateSut(
        Mock<IUserRoleService>? userRoleService = null,
        Mock<IUserCredentialQueryService>? credentialQuery = null,
        Mock<ITokenService>? tokenService = null,
        IMemoryCache? cache = null,
        IConfiguration? configuration = null)
    {
        userRoleService ??= new Mock<IUserRoleService>();
        credentialQuery ??= new Mock<IUserCredentialQueryService>();
        tokenService ??= new Mock<ITokenService>();
        cache ??= new MemoryCache(new MemoryCacheOptions());
        configuration ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshPepper"] = "test-pepper" })
            .Build();

        return new AdminTotpService(
            userRoleService.Object,
            credentialQuery.Object,
            new Mock<ICommandService<UserCredential, int>>().Object,
            tokenService.Object,
            new Mock<IPasswordHasher<User>>().Object,
            new Mock<IUserQueryService>().Object,
            cache,
            configuration);
    }

    [Fact]
    public async Task ApplyAdminTotpGateAsync_WhenNotAdmin_IssuesTokensDirectly()
    {
        var user = new User { Id = 1, DisplayName = "User", Email = "u@example.com" };
        var userRoleService = new Mock<IUserRoleService>();
        userRoleService.Setup(r => r.GetUserRoleNameAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync("VpnUser");

        var sut = CreateSut(userRoleService);
        var expected = new LoginResponse { UserId = 1, Token = "access" };

        var result = await sut.ApplyAdminTotpGateAsync(
            user,
            credential: null,
            externalId: null,
            deviceId: null,
            userAgent: null,
            _ => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Equal("access", result.Token);
        Assert.False(result.RequiresTotp);
    }

    [Fact]
    public async Task ApplyAdminTotpGateAsync_WhenAdminWithoutTotp_ReturnsRequiresTotpSetup()
    {
        var user = new User { Id = 2, DisplayName = "Admin", Email = "admin@example.com" };
        var userRoleService = new Mock<IUserRoleService>();
        userRoleService.Setup(r => r.GetUserRoleNameAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync("Admin");

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.IssueAsync(2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("access", DateTime.UtcNow.AddHours(1), "refresh", DateTime.UtcNow.AddDays(1)));

        var sut = CreateSut(userRoleService, tokenService: tokenService);

        var result = await sut.ApplyAdminTotpGateAsync(
            user,
            credential: null,
            externalId: null,
            deviceId: null,
            userAgent: null,
            cancel => Task.FromResult(new LoginResponse
            {
                UserId = user.Id,
                Token = "access",
                RefreshToken = "refresh",
            }),
            CancellationToken.None);

        Assert.True(result.RequiresTotpSetup);
        Assert.Equal("access", result.Token);
    }

    [Fact]
    public async Task ApplyAdminTotpGateAsync_WhenAdminWithTotpEnabled_ReturnsChallenge()
    {
        var user = new User { Id = 3, DisplayName = "Admin", Email = "admin2@example.com" };
        var credential = new UserCredential
        {
            UserId = 3,
            TotpEnabledAt = DateTimeOffset.UtcNow,
            TotpSecretEncrypted = "encrypted",
        };

        var userRoleService = new Mock<IUserRoleService>();
        userRoleService.Setup(r => r.GetUserRoleNameAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync("Admin");

        var sut = CreateSut(userRoleService);

        var result = await sut.ApplyAdminTotpGateAsync(
            user,
            credential,
            externalId: "ext",
            deviceId: "dev",
            userAgent: "agent",
            _ => Task.FromResult(new LoginResponse { Token = "should-not-be-used" }),
            CancellationToken.None);

        Assert.True(result.RequiresTotp);
        Assert.False(string.IsNullOrWhiteSpace(result.LoginChallengeId));
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task GetStatusAsync_WhenNotAdmin_ReturnsDisabledStatus()
    {
        var userRoleService = new Mock<IUserRoleService>();
        userRoleService.Setup(r => r.GetUserRoleNameAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync("VpnUser");

        var sut = CreateSut(userRoleService);

        var status = await sut.GetStatusAsync(5, CancellationToken.None);

        Assert.False(status.IsAdmin);
        Assert.False(status.TotpEnabled);
        Assert.False(status.RequiresTotpSetup);
    }

    [Fact]
    public async Task GetStatusAsync_WhenAdminWithoutTotp_RequiresSetup()
    {
        var userRoleService = new Mock<IUserRoleService>();
        userRoleService.Setup(r => r.GetUserRoleNameAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync("Admin");

        var credentialQuery = new Mock<IUserCredentialQueryService>();
        credentialQuery.Setup(q => q.GetByUserId(6, It.IsAny<CancellationToken>())).ReturnsAsync((UserCredential?)null);

        var sut = CreateSut(userRoleService, credentialQuery);

        var status = await sut.GetStatusAsync(6, CancellationToken.None);

        Assert.True(status.IsAdmin);
        Assert.False(status.TotpEnabled);
        Assert.True(status.RequiresTotpSetup);
    }

    [Fact]
    public void IsTotpEnabled_WhenSecretAndTimestampPresent_ReturnsTrue()
    {
        var sut = CreateSut();
        var credential = new UserCredential
        {
            TotpEnabledAt = DateTimeOffset.UtcNow,
            TotpSecretEncrypted = "secret",
        };

        Assert.True(sut.IsTotpEnabled(credential));
        Assert.False(sut.IsTotpEnabled(null));
    }

    [Fact]
    public async Task VerifyLoginChallengeAsync_WhenBlankChallengeOrCode_ThrowsUnauthorized()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.VerifyLoginChallengeAsync(
                new TotpVerifyLoginRequest { LoginChallengeId = " ", Code = "" },
                CancellationToken.None));
    }
}
