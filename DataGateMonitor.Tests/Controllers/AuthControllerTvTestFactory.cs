using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.TelegramLogin;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.Services.Api.CurrentUser.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;

namespace DataGateMonitor.Tests.Controllers;

internal static class AuthControllerTvTestFactory
{
    public static AuthController Create(Mock<ITvLoginSessionService> tvLogin)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(1);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "VeryStrongTestSecretKey1234567890",
                ["Jwt:AdminIdleTimeoutMinutes"] = "20",
            })
            .Build();

        var controller = new AuthController(
            config,
            Mock.Of<IApplicationService>(),
            Mock.Of<IMicroserviceTokenService>(),
            Mock.Of<IUserRegistrationService>(),
            Mock.Of<IUserLoginService>(),
            Mock.Of<IUserQueryService>(),
            Mock.Of<IEmailConfirmationService>(),
            Mock.Of<ITelegramAccountLinkService>(),
            Mock.Of<IFreeTierAccessComplianceService>(),
            Mock.Of<IGoogleAuthCodeExchangeService>(),
            Mock.Of<ITokenService>(),
            Mock.Of<IAdminForgotPasswordService>(),
            Mock.Of<ITelegramLoginCodeService>(),
            tvLogin.Object,
            Mock.Of<IAdminTotpService>(),
            currentUser.Object,
            Mock.Of<IAdminIdleSessionTracker>(),
            Mock.Of<IUserSessionService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        // Stash current-user mock for SetCurrentUserId
        controller.HttpContext.Items["__currentUserMock"] = currentUser;
        return controller;
    }

    public static void SetCurrentUserId(AuthController controller, int userId)
    {
        var mock = (Mock<ICurrentUserService>)controller.HttpContext.Items["__currentUserMock"]!;
        mock.SetupGet(c => c.UserId).Returns(userId);
    }
}
