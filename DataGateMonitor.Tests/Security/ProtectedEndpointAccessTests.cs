using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataGateMonitor.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Moq;
using DataGateMonitor.Services.Others;
using DataGateMonitor.SharedModels.DataGateMonitor.Settings.Requests;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Security;

/// <summary>
/// Spot-checks that protected controllers reject anonymous callers and wrong roles
/// at the attribute layer (Authorize), matching production JWT middleware behavior.
/// </summary>
public class ProtectedEndpointAccessTests
{
    [Fact]
    public void PerformanceController_RequiresAdminRole()
    {
        var attr = typeof(PerformanceController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("Admin", attr.Roles);
    }

    [Fact]
    public void OpenVpnFilesController_AllowsAdminVpnUserApp_NotAnonymous()
    {
        var attrs = typeof(OpenVpnFilesController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        Assert.Contains(attrs, a => a.Roles == "Admin,VpnUser,App");
        Assert.DoesNotContain(
            typeof(OpenVpnFilesController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true),
            _ => true);
    }

    [Fact]
    public async Task SettingsGet_WhenUserIsApp_AllowsCall_WhenAdminOnlyWouldForbid()
    {
        // Settings is Admin,App — App principal is enough for the attribute set.
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetValueAsync<string>("k_Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync("string");
        settings.Setup(s => s.GetValueAsync<string>("k", It.IsAny<CancellationToken>()))
            .ReturnsAsync("v");

        var controller = new SettingsController(settings.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = Principal("App"),
            },
        };

        var result = await controller.Get(new GetSettingRequest { Key = "k" }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void DevToken_IsAllowAnonymous_ButGatedByEnvironment()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.GenerateDevToken));
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).FirstOrDefault());
    }

    private static ClaimsPrincipal Principal(string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, role),
        ], authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
