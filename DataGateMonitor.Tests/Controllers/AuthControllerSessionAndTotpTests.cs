using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.TelegramLogin;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.Services.Api.CurrentUser.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.Extensions.Configuration;

namespace DataGateMonitor.Tests.Controllers;

public class AuthControllerSessionAndTotpTests
{
    private readonly Mock<IApplicationService> _appService = new();
    private readonly Mock<IMicroserviceTokenService> _microserviceTokenService = new();
    private readonly Mock<IUserRegistrationService> _userRegistrationService = new();
    private readonly Mock<IUserLoginService> _userLoginService = new();
    private readonly Mock<IUserQueryService> _userQueryService = new();
    private readonly Mock<IEmailConfirmationService> _emailConfirmationService = new();
    private readonly Mock<ITelegramAccountLinkService> _telegramAccountLinkService = new();
    private readonly Mock<IFreeTierAccessComplianceService> _freeTierComplianceService = new();
    private readonly Mock<IGoogleAuthCodeExchangeService> _exchange = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IAdminForgotPasswordService> _adminForgotPasswordService = new();
    private readonly Mock<ITelegramLoginCodeService> _telegramLoginCodeService = new();
    private readonly Mock<ITvLoginSessionService> _tvLoginSessionService = new();
    private readonly Mock<IAdminTotpService> _adminTotpService = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly Mock<IAdminIdleSessionTracker> _adminIdleSessionTracker = new();
    private readonly Mock<IAdminIdleTimeoutProvider> _adminIdleTimeoutProvider = new();
    private readonly Mock<IUserSessionService> _userSessionService = new();

    private AuthController CreateController(HttpContext? httpContext = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "VeryStrongTestSecretKey1234567890",
                ["Jwt:AdminIdleTimeoutMinutes"] = "20",
            })
            .Build();

        _adminIdleTimeoutProvider
            .Setup(p => p.GetMinutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(20);

        var controller = new AuthController(
            config,
            _appService.Object,
            _microserviceTokenService.Object,
            _userRegistrationService.Object,
            _userLoginService.Object,
            _userQueryService.Object,
            _emailConfirmationService.Object,
            _telegramAccountLinkService.Object,
            _freeTierComplianceService.Object,
            _exchange.Object,
            _tokenService.Object,
            _adminForgotPasswordService.Object,
            _telegramLoginCodeService.Object,
            _tvLoginSessionService.Object,
            _adminTotpService.Object,
            _currentUserService.Object,
            _adminIdleSessionTracker.Object,
            _adminIdleTimeoutProvider.Object,
            _userSessionService.Object);

        if (httpContext != null)
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    [Fact]
    public async Task ForgotPassword_DelegatesWithClientIp()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");
        _adminForgotPasswordService
            .Setup(s => s.RequestResetCodeAsync(
                It.Is<AdminForgotPasswordRequest>(r => r.LoginOrEmail == "admin"),
                "203.0.113.10",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminForgotPasswordResponse { Message = "sent" });

        var controller = CreateController(http);
        var result = await controller.ForgotPassword(
            new AdminForgotPasswordRequest { LoginOrEmail = "admin" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<AdminForgotPasswordResponse>>(ok.Value);
        Assert.Equal("sent", payload.Data!.Message);
    }

    [Fact]
    public async Task ResetPassword_DelegatesToService()
    {
        _adminForgotPasswordService
            .Setup(s => s.ResetPasswordAsync(
                It.Is<AdminResetPasswordRequest>(r => r.Code == "ABC" && r.NewPassword == "n"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminResetPasswordResponse { Success = true, Message = "ok" });

        var result = await CreateController().ResetPassword(
            new AdminResetPasswordRequest { Code = "ABC", NewPassword = "n", ConfirmPassword = "n" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<AdminResetPasswordResponse>>(ok.Value).Data!.Success);
    }

    [Fact]
    public async Task TelegramCodeLogin_DelegatesToService()
    {
        _telegramLoginCodeService
            .Setup(s => s.LoginWithCodeAsync(
                It.Is<TelegramCodeLoginRequest>(r => r.Code == "ABCD1234"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { Token = "t" });

        var result = await CreateController().TelegramCodeLogin(
            new TelegramCodeLoginRequest { Code = "ABCD1234" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("t", Assert.IsType<ApiResponse<LoginResponse>>(ok.Value).Data!.Token);
    }

    [Fact]
    public async Task Refresh_MapsTokenServiceResult()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var refreshExpires = DateTimeOffset.UtcNow.AddDays(7);
        _tokenService
            .Setup(s => s.RefreshAsync("old-r", "dev", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("access", expires, "new-r", refreshExpires));

        var result = await CreateController().Refresh(
            new RefreshRequest { RefreshToken = "old-r", DeviceId = "dev", UserAgent = "ua" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var data = Assert.IsType<ApiResponse<RefreshResponse>>(ok.Value).Data!;
        Assert.Equal("access", data.Token);
        Assert.Equal("new-r", data.RefreshToken);
        Assert.Equal(expires, data.Expiration);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_RevokesAndReturnsOk()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), "UserCookie", null))
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(auth.Object);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        _userSessionService
            .Setup(s => s.RevokeByRefreshTokenAsync("rt", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController(http).Logout(
            new RefreshRequest { RefreshToken = "rt" },
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        _userSessionService.Verify(s => s.RevokeByRefreshTokenAsync("rt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeSession_ReturnsNoContent()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        _userSessionService
            .Setup(s => s.RevokeSessionAsync(5, 99, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController().RevokeSession(99, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RevokeOtherSessions_PassesKeepRefreshToken()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        _userSessionService
            .Setup(s => s.RevokeSessionsAsync(5, "keep-me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await CreateController().RevokeOtherSessions(
            new RevokeUserSessionsRequest { KeepRefreshToken = "keep-me" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(3, Assert.IsType<ApiResponse<int>>(ok.Value).Data);
    }

    [Fact]
    public async Task GetSessions_UsesXRefreshTokenHeader()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Refresh-Token"] = "hdr-rt";
        _userSessionService
            .Setup(s => s.GetActiveSessionsAsync(5, "hdr-rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserSessionsResponse { Sessions = [] });

        var result = await CreateController(http).GetSessions(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<GetUserSessionsResponse>>(ok.Value).Success);
    }

    [Fact]
    public async Task VerifyTotpLogin_DelegatesToAdminTotp()
    {
        _adminTotpService
            .Setup(s => s.VerifyLoginChallengeAsync(
                It.Is<TotpVerifyLoginRequest>(r => r.LoginChallengeId == "c1" && r.Code == "123456"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { Token = "totp-t" });

        var result = await CreateController().VerifyTotpLogin(
            new TotpVerifyLoginRequest { LoginChallengeId = "c1", Code = "123456" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("totp-t", Assert.IsType<ApiResponse<LoginResponse>>(ok.Value).Data!.Token);
    }

    [Fact]
    public async Task BeginTotpSetup_ReturnsSetupForCurrentUser()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        _adminTotpService
            .Setup(s => s.BeginSetupAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TotpSetupResponse
            {
                SharedSecret = "sec",
                OtpAuthUri = "otpauth://",
                Issuer = "DataGate",
                AccountName = "admin"
            });

        var result = await CreateController().BeginTotpSetup(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("sec", Assert.IsType<ApiResponse<TotpSetupResponse>>(ok.Value).Data!.SharedSecret);
    }

    [Fact]
    public async Task Register_DelegatesToUserRegistrationService()
    {
        _userRegistrationService
            .Setup(s => s.RegisterAsync(
                It.Is<RegisterUserRequest>(r => r.Login == "newuser"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterUserResponse
            {
                UserId = 11,
                DisplayName = "New",
                Email = "n@ex.com",
                HasDashboardAccess = true
            });

        var result = await CreateController().Register(
            new RegisterUserRequest
            {
                DisplayName = "New",
                Email = "n@ex.com",
                Login = "newuser",
                Password = "p",
                ConfirmPassword = "p"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(11, Assert.IsType<ApiResponse<RegisterUserResponse>>(ok.Value).Data!.UserId);
    }

    [Fact]
    public async Task GoogleCodeLogin_WhenRequestNull_ReturnsBadRequest()
    {
        var result = await CreateController().GoogleCodeLogin(null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GoogleCodeLogin_ExchangesCodeThenLogsIn()
    {
        _exchange
            .Setup(e => e.ExchangeCodeForIdTokenAsync("code", "verifier", "https://app/cb", It.IsAny<CancellationToken>()))
            .ReturnsAsync("id-token");
        _userLoginService
            .Setup(s => s.LoginWithGoogleAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleLoginResponse { Token = "g-tok", UserId = 3 });

        var result = await CreateController().GoogleCodeLogin(
            new GoogleCodeLoginRequest
            {
                Code = "code",
                CodeVerifier = "verifier",
                RedirectUri = "https://app/cb"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("g-tok", Assert.IsType<ApiResponse<GoogleLoginResponse>>(ok.Value).Data!.Token);
        _exchange.VerifyAll();
        _userLoginService.VerifyAll();
    }

    [Fact]
    public async Task ConfirmTotpSetup_DelegatesForCurrentUser()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        _adminTotpService
            .Setup(s => s.ConfirmSetupAsync(
                5,
                It.Is<TotpConfirmRequest>(r => r.Code == "123456"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController().ConfirmTotpSetup(
            new TotpConfirmRequest { Code = "123456" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Contains("enabled", Assert.IsType<ApiResponse<string>>(ok.Value).Data);
    }

    [Fact]
    public async Task DisableTotp_DelegatesForCurrentUser()
    {
        _currentUserService.Setup(s => s.UserId).Returns(5);
        _adminTotpService
            .Setup(s => s.DisableAsync(
                5,
                It.Is<TotpDisableRequest>(r => r.Code == "123456" && r.Password == "pw"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController().DisableTotp(
            new TotpDisableRequest { Code = "123456", Password = "pw" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Contains("disabled", Assert.IsType<ApiResponse<string>>(ok.Value).Data);
    }
}
