using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using DataGateMonitor.Configurations;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using Xunit;

namespace DataGateMonitor.Tests.Configurations;

public class JwtBearerEventHandlersTests
{
    private static TokenValidatedContext CreateContext(
        ClaimsPrincipal principal,
        IApplicationService? appService)
    {
        var services = new ServiceCollection();
        if (appService != null)
            services.AddSingleton(appService);

        var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        return new TokenValidatedContext(http, scheme, new JwtBearerOptions())
        {
            Principal = principal,
        };
    }

    private static ClaimsPrincipal AppPrincipal(string? clientId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "App") };
        if (clientId != null)
            claims.Add(new Claim(ClaimTypes.Name, clientId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static ClaimsPrincipal AdminPrincipal()
        => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Name, "admin-user"),
        ], "Bearer"));

    [Fact]
    public async Task RejectRevokedAppClient_Ignores_NonApp_Roles()
    {
        var appService = new Mock<IApplicationService>(MockBehavior.Strict);
        var context = CreateContext(AdminPrincipal(), appService.Object);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.Null(context.Result);
        appService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectRevokedAppClient_Fails_When_App_Missing_ClientId()
    {
        var appService = new Mock<IApplicationService>(MockBehavior.Strict);
        var context = CreateContext(AppPrincipal(null), appService.Object);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.NotNull(context.Result);
        Assert.False(context.Result!.Succeeded);
        Assert.Equal(JwtBearerEventHandlers.AppClientRevokedFailure, context.Result.Failure?.Message);
        appService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectRevokedAppClient_Fails_When_Client_NotFound()
    {
        var appService = new Mock<IApplicationService>();
        appService
            .Setup(s => s.GetApplicationByClientIdAsync("cid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplication?)null);
        var context = CreateContext(AppPrincipal("cid"), appService.Object);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.NotNull(context.Result);
        Assert.False(context.Result!.Succeeded);
    }

    [Fact]
    public async Task RejectRevokedAppClient_Fails_When_Client_Revoked()
    {
        var appService = new Mock<IApplicationService>();
        appService
            .Setup(s => s.GetApplicationByClientIdAsync("cid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientApplication { ClientId = "cid", IsRevoked = true });
        var context = CreateContext(AppPrincipal("cid"), appService.Object);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.NotNull(context.Result);
        Assert.False(context.Result!.Succeeded);
        Assert.Equal(JwtBearerEventHandlers.AppClientRevokedFailure, context.Result.Failure?.Message);
    }

    [Fact]
    public async Task RejectRevokedAppClient_Allows_Active_App_Client()
    {
        var appService = new Mock<IApplicationService>();
        appService
            .Setup(s => s.GetApplicationByClientIdAsync("cid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientApplication { ClientId = "cid", IsRevoked = false });
        var context = CreateContext(AppPrincipal("cid"), appService.Object);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task RejectRevokedAppClient_Fails_When_AppService_Missing_From_DI()
    {
        var context = CreateContext(AppPrincipal("cid"), appService: null);

        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);

        Assert.NotNull(context.Result);
        Assert.False(context.Result!.Succeeded);
    }

    [Fact]
    public void IsExpectedClientTokenFailure_Detects_Expired()
    {
        Assert.True(JwtBearerEventHandlers.IsExpectedClientTokenFailure(
            new SecurityTokenExpiredException("expired")));
        Assert.False(JwtBearerEventHandlers.IsExpectedClientTokenFailure(
            new InvalidOperationException("boom")));
    }
}
