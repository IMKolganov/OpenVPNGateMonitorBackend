using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserCredentialTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.EmailTemplates;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.Api.Auth.ForgotPassword;

public class AdminForgotPasswordServiceTests
{
    private static AdminForgotPasswordService CreateSut() =>
        new(
            Mock.Of<IUserCredentialQueryService>(),
            Mock.Of<IUserQueryService>(),
            Mock.Of<ICommandService<UserCredential, int>>(),
            Mock.Of<IUserPasswordHistoryService>(),
            Mock.Of<IPasswordHasher<User>>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IEmailSenderService>(),
            Mock.Of<ISentEmailLogService>(),
            Mock.Of<ISystemTransactionalEmailService>(),
            Mock.Of<IAppNotificationFacade>(),
            NullLogger<AdminForgotPasswordService>.Instance);

    [Fact]
    public async Task ResetPasswordAsync_WhenCodeMissing_Fails()
    {
        var result = await CreateSut().ResetPasswordAsync(
            new AdminResetPasswordRequest { Code = "  ", NewPassword = "longenough", ConfirmPassword = "longenough" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Invalid or expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordsMismatch_Fails()
    {
        var result = await CreateSut().ResetPasswordAsync(
            new AdminResetPasswordRequest
            {
                Code = "ABCDEFGHIJ",
                NewPassword = "longenough",
                ConfirmPassword = "different1",
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("do not match", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
