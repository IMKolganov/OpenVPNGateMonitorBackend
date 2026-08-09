using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

public class AuthControllerTests
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
    private readonly Mock<IUserSessionService> _userSessionService = new();

    private AuthController CreateController()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "VeryStrongTestSecretKey1234567890",
                ["Jwt:AdminIdleTimeoutMinutes"] = "20",
            })
            .Build();

        return new AuthController(
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
            _userSessionService.Object);
    }

    [Fact]
    public void GetSessionPolicy_ReturnsConfiguredTimeout()
    {
        var controller = CreateController();

        var result = controller.GetSessionPolicy();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AuthSessionPolicyResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(20, response.Data!.AdminIdleTimeoutMinutes);
    }

    [Fact]
    public async Task GenerateToken_WhenAppMissing_ReturnsUnauthorized()
    {
        _appService
            .Setup(s => s.GetApplicationByClientIdAsync("client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplication?)null);

        var controller = CreateController();
        var result = await controller.GenerateToken(
            new TokenRequest { ClientId = "client", ClientSecret = "secret" },
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<TokenResponse>>(unauthorized.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Login_DelegatesToUserLoginService()
    {
        var loginResponse = new LoginResponse { UserId = 1, Token = "jwt" };
        _userLoginService
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);

        var controller = CreateController();
        var request = new LoginRequest { Login = "admin", Password = "pass" };
        var result = await controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<LoginResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("jwt", response.Data!.Token);
    }

    [Fact]
    public async Task RequestEmailConfirmation_WhenEmailMissing_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.RequestEmailConfirmation(
            new RequestEmailConfirmationRequest { Email = "  " },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(bad.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task RequestEmailConfirmation_WhenUserUnconfirmed_SendsEmail()
    {
        _userQueryService
            .Setup(q => q.GetByEmail("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Email = "user@example.com", IsEmailConfirmed = false });

        var controller = CreateController();
        var result = await controller.RequestEmailConfirmation(
            new RequestEmailConfirmationRequest { Email = "user@example.com" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(((ApiResponse<string>)ok.Value!).Success);
        _emailConfirmationService.Verify(
            s => s.SendConfirmationAsync(3, "user@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GoogleLogin_ReturnsMappedResponse()
    {
        _userLoginService
            .Setup(s => s.LoginWithGoogleAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleLoginResponse { UserId = 9, Token = "google-jwt", IsNewUser = false });

        var controller = CreateController();
        var result = await controller.GoogleLogin(
            new GoogleLoginRequest { IdToken = "id-token" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<GoogleLoginResponse>>(ok.Value);
        Assert.Equal(9, response.Data!.UserId);
    }

    [Fact]
    public async Task TelegramRequestLoginCode_WhenUserMissing_ReturnsNotFound()
    {
        _telegramLoginCodeService
            .Setup(s => s.RequestLoginCodeAsync(It.IsAny<TelegramRequestLoginCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramRequestLoginCodeResponse?)null);

        var controller = CreateController();
        var result = await controller.TelegramRequestLoginCode(
            new TelegramRequestLoginCodeRequest { TelegramId = 1 },
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.False(((ApiResponse<TelegramRequestLoginCodeResponse>)notFound.Value!).Success);
    }

    [Fact]
    public void GetPublicKeyForMicroservice_WhenPinTooLow_ReturnsBadRequest()
    {
        _microserviceTokenService.Setup(s => s.GetPublicKeyPem()).Returns("pem-key");

        var controller = CreateController();
        var result = controller.GetPublicKeyForMicroservice(100);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(((ApiResponse<string>)bad.Value!).Success);
    }

    [Fact]
    public async Task RequestTelegramAccountLinkCode_DelegatesToService()
    {
        _currentUserService.Setup(s => s.UserId).Returns(42);
        _telegramAccountLinkService
            .Setup(s => s.RequestLinkCodeAsync(42, 999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestTelegramAccountLinkCodeResponse
            {
                Code = "ABCD2345",
                ExpiresInSeconds = 900,
            });

        var controller = CreateController();
        var result = await controller.RequestTelegramAccountLinkCode(
            new RequestTelegramAccountLinkCodeRequest { TelegramId = 999 },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RequestTelegramAccountLinkCodeResponse>>(ok.Value);
        Assert.Equal("ABCD2345", response.Data!.Code);
    }

    [Fact]
    public async Task RequestTelegramAccountLinkCode_WhenTelegramIdOmitted_DelegatesWithNull()
    {
        _currentUserService.Setup(s => s.UserId).Returns(42);
        _telegramAccountLinkService
            .Setup(s => s.RequestLinkCodeAsync(42, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestTelegramAccountLinkCodeResponse
            {
                Code = "WXYZ5678",
                ExpiresInSeconds = 900,
            });

        var controller = CreateController();
        var result = await controller.RequestTelegramAccountLinkCode(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RequestTelegramAccountLinkCodeResponse>>(ok.Value);
        Assert.Equal("WXYZ5678", response.Data!.Code);
    }

    [Fact]
    public async Task RequestTelegramAccountLinkCode_WhenBusinessRuleFails_ReturnsBadRequest()
    {
        _currentUserService.Setup(s => s.UserId).Returns(42);
        _telegramAccountLinkService
            .Setup(s => s.RequestLinkCodeAsync(42, 999L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("already linked"));

        var controller = CreateController();
        var result = await controller.RequestTelegramAccountLinkCode(
            new RequestTelegramAccountLinkCodeRequest { TelegramId = 999 },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(((ApiResponse<RequestTelegramAccountLinkCodeResponse>)bad.Value!).Success);
    }

    [Fact]
    public async Task GetFreeTierAccessStatus_DelegatesToComplianceService()
    {
        _currentUserService.Setup(s => s.UserId).Returns(7);
        _freeTierComplianceService
            .Setup(s => s.GetStatusAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessStatusResponse
            {
                IsApplicable = true,
                IsCompliant = false,
                CanRequestAccountLinkCode = true,
                RequiredChannel = "@DataGateVPNBot",
                ActivePlanName = "Free",
            });

        var controller = CreateController();
        var result = await controller.GetFreeTierAccessStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<FreeTierAccessStatusResponse>>(ok.Value);
        Assert.True(response.Data!.IsApplicable);
        Assert.False(response.Data.IsCompliant);
        Assert.True(response.Data.CanRequestAccountLinkCode);
    }

    [Fact]
    public async Task RegisterFreeTierAccessConnect_DelegatesToComplianceServiceForCurrentUser()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _currentUserService.Setup(s => s.UserId).Returns(9);
        _freeTierComplianceService
            .Setup(s => s.RegisterConnectionAsync(9, "android-connect", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FreeTierAccessStatusResponse
            {
                IsApplicable = true,
                IsCompliant = true,
                IsGracePeriod = true,
                GraceExpiresAtUtc = expiresAt,
                RequiredChannel = "@DataGateVPNBot",
                ActivePlanName = "Free",
            });

        var controller = CreateController();
        var result = await controller.RegisterFreeTierAccessConnect(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<FreeTierAccessStatusResponse>>(ok.Value);
        Assert.True(response.Data!.IsGracePeriod);
        Assert.Equal(expiresAt, response.Data.GraceExpiresAtUtc);
        _freeTierComplianceService.Verify(
            s => s.RegisterConnectionAsync(9, "android-connect", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_DelegatesToEmailConfirmationService()
    {
        _emailConfirmationService
            .Setup(s => s.ConfirmAsync("user@example.com", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmEmailResponse { Success = true, Message = "Email confirmed." });

        var controller = CreateController();
        var result = await controller.ConfirmEmail(
            new ConfirmEmailRequest { Email = "user@example.com", Code = "123456" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ConfirmEmailResponse>>(ok.Value);
        Assert.True(response.Data!.Success);
    }
}
