using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.Users;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.Users;

public class UserPasswordHistoryServiceTests
{
    private static UserPasswordHistoryService CreateSut(
        Mock<IUserCredentialQueryService>? credentials = null)
    {
        return new UserPasswordHistoryService(
            Mock.Of<IUnitOfWork>(),
            (credentials ?? new Mock<IUserCredentialQueryService>()).Object,
            Mock.Of<IUserQueryService>(),
            Mock.Of<ICommandService<UserCredential, int>>(),
            Mock.Of<ICommandService<UserPasswordHistory, int>>(),
            Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<IAppNotificationFacade>(),
            NullLogger<UserPasswordHistoryService>.Instance);
    }

    [Fact]
    public async Task AdminSetPasswordAsync_WhenPasswordTooShort_Fails()
    {
        var sut = CreateSut();
        var result = await sut.AdminSetPasswordAsync(
            1,
            2,
            new AdminSetUserPasswordRequest { NewPassword = "short" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("8 characters", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminSetPasswordAsync_WhenNoCredential_Fails()
    {
        var credentials = new Mock<IUserCredentialQueryService>();
        credentials.Setup(c => c.GetByUserId(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCredential?)null);

        var sut = CreateSut(credentials);
        var result = await sut.AdminSetPasswordAsync(
            9,
            2,
            new AdminSetUserPasswordRequest { NewPassword = "longenough" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no password credential", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
