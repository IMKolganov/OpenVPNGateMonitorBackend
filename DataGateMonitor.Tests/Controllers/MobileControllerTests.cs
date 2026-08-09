using System.Security.Claims;
using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Mobile.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.Mobile.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Mobile.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class MobileControllerTests
{
    [Fact]
    public async Task AddAndroidInstallationId_ReturnsOk_WhenUserIdValid()
    {
        var devices = new Mock<IDeviceService>();
        devices.Setup(d => d.AddAndroidInstallationId(
                It.Is<InstallationIdRequest>(r => r.InstallationId == "install-1"),
                42,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstallationIdResponse
            {
                Application = new DeviceDto { InstallationId = "install-1" }
            });

        var controller = new MobileController(devices.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "42")],
                        "mock"))
                }
            }
        };

        var result = await controller.AddAndroidInstallationId(
            new InstallationIdRequest { InstallationId = "install-1" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<InstallationIdResponse>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("install-1", payload.Data!.Application.InstallationId);
        devices.VerifyAll();
    }

    [Fact]
    public async Task AddAndroidInstallationId_ReturnsUnauthorized_WhenUserIdMissing()
    {
        var devices = new Mock<IDeviceService>(MockBehavior.Strict);
        var controller = new MobileController(devices.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([], "mock"))
                }
            }
        };

        var result = await controller.AddAndroidInstallationId(
            new InstallationIdRequest { InstallationId = "x" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
