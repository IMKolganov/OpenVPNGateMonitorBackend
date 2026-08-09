using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;

namespace DataGateMonitor.Tests.Services.Api.Auth.Totp;

public class AdminTotpServiceTests
{
    private static AdminTotpService CreateSut(Mock<IUserRoleService>? roles = null) =>
        new(
            (roles ?? new Mock<IUserRoleService>()).Object,
            Mock.Of<IUserCredentialQueryService>(),
            Mock.Of<ICommandService<UserCredential, int>>(),
            Mock.Of<ITokenService>(),
            Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<IUserQueryService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new ConfigurationBuilder().Build());

    [Fact]
    public void IsTotpEnabled_WhenNullCredential_ReturnsFalse()
    {
        Assert.False(CreateSut().IsTotpEnabled(null));
    }

    [Fact]
    public async Task VerifyLoginChallengeAsync_WhenBlank_ThrowsUnauthorized()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.VerifyLoginChallengeAsync(
                new TotpVerifyLoginRequest { LoginChallengeId = " ", Code = "" },
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAdminTotpGateAsync_WhenNotAdmin_IssuesTokensDirectly()
    {
        var roles = new Mock<IUserRoleService>();
        roles.Setup(r => r.GetUserRoleNameAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync("User");

        var issued = false;
        var result = await CreateSut(roles).ApplyAdminTotpGateAsync(
            new User { Id = 5, DisplayName = "u" },
            null,
            null,
            null,
            null,
            _ =>
            {
                issued = true;
                return Task.FromResult(new SharedModels.DataGateMonitor.Auth.Responses.LoginResponse
                {
                    UserId = 5,
                    Token = "a",
                });
            },
            CancellationToken.None);

        Assert.True(issued);
        Assert.Equal(5, result.UserId);
        Assert.False(result.RequiresTotp);
    }
}
