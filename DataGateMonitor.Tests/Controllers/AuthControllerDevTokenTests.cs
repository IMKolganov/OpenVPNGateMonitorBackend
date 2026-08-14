using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models.Auth;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.TelegramLogin;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.Services.Api.CurrentUser.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers;

public class AuthControllerDevTokenTests
{
    private readonly Mock<IAdminIdleSessionTracker> _adminIdleSessionTracker = new();
    private readonly Mock<IMicroserviceTokenService> _microserviceTokenService = new();

    private AuthController CreateController(string environmentName, bool enabled)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "VeryStrongTestSecretKey1234567890",
            ["Jwt:AdminIdleTimeoutMinutes"] = "15",
            ["DevAuth:Enabled"] = enabled ? "true" : "false",
            ["DevAuth:AppClientId"] = "dev-loadtest-app",
            ["DevAuth:AdminUserId"] = "42",
            ["DevAuth:AdminDisplayName"] = "Dev Admin",
            ["DevAuth:AdminEmail"] = "dev-admin@localhost",
            ["DevAuth:LifetimeMinutes"] = "30",
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);

        _microserviceTokenService
            .Setup(s => s.GenerateToken("vpn-cert-issuer", "cert-create", "backend", "DataGateOpenVpnManager"))
            .Returns("microservice.jwt.token");
        _microserviceTokenService
            .Setup(s => s.GetPublicKeyPem())
            .Returns("-----BEGIN PUBLIC KEY-----\nTEST\n-----END PUBLIC KEY-----");

        return new AuthController(
            config,
            env.Object,
            Mock.Of<IApplicationService>(),
            _microserviceTokenService.Object,
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
            Mock.Of<ITvLoginSessionService>(),
            Mock.Of<IAdminTotpService>(),
            Mock.Of<ICurrentUserService>(),
            _adminIdleSessionTracker.Object,
            Mock.Of<IUserSessionService>());
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void GenerateDevToken_WhenNotDevelopmentOrDisabled_ReturnsNotFound(string env, bool enabled)
    {
        var result = CreateController(env, enabled).GenerateDevToken(new DevTokenRequest { Role = "OpenVpn" });
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GenerateDevToken_WhenOpenVpn_ReturnsMicroserviceJwtAndPublicKey()
    {
        var result = CreateController(Environments.Development, enabled: true)
            .GenerateDevToken(new DevTokenRequest { Role = "OpenVpn" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<DevTokenResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("microservice.jwt.token", payload.Data!.Token);
        Assert.Equal("OpenVpn", payload.Data.Role);
        Assert.Equal("DataGateOpenVpnManager", payload.Data.Audience);
        Assert.Equal("cert-create", payload.Data.Purpose);
        Assert.Equal("backend", payload.Data.MicroserviceRole);
        Assert.Contains("BEGIN PUBLIC KEY", payload.Data.PublicKeyPem);
        _microserviceTokenService.Verify(
            s => s.GenerateToken("vpn-cert-issuer", "cert-create", "backend", "DataGateOpenVpnManager"),
            Times.Once);
    }

    [Fact]
    public void GenerateDevToken_WhenApp_ReturnsAppJwt()
    {
        var result = CreateController(Environments.Development, enabled: true)
            .GenerateDevToken(new DevTokenRequest { Role = "App" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<DevTokenResponse>>(ok.Value);
        Assert.Equal("App", payload.Data!.Role);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Data.Token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "App");
    }

    [Fact]
    public void GenerateDevToken_WhenAdmin_ReturnsAdminJwtAndTouchesIdleTracker()
    {
        var result = CreateController(Environments.Development, enabled: true)
            .GenerateDevToken(new DevTokenRequest { Role = "Admin" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<DevTokenResponse>>(ok.Value);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(payload.Data!.Token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        _adminIdleSessionTracker.Verify(t => t.Touch(42), Times.Once);
    }

    [Fact]
    public void GenerateDevToken_WhenInvalidRole_ReturnsBadRequest()
    {
        var result = CreateController(Environments.Development, enabled: true)
            .GenerateDevToken(new DevTokenRequest { Role = "VpnUser" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
