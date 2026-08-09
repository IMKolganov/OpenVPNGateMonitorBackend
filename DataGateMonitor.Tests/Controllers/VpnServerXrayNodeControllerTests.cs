using System.Security.Claims;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.SharedModels.DataGateMonitor.XrayNode.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.XrayNode.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class VpnServerXrayNodeControllerTests
{
    [Fact]
    public async Task KickUser_ReturnsOk_ForXrayServer()
    {
        var (controller, xrayClient) = CreateController(new VpnServer
        {
            Id = 11,
            ServerName = "xray",
            ApiUrl = "https://xray.example/",
            ServerType = VpnServerType.Xray
        });

        var result = await controller.KickUser(
            11,
            new XrayNodeUserActionRequest { CommonName = "cn-1" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<XrayNodeUserActionResponse>>(ok.Value).Success);
        xrayClient.Verify(c => c.KickUserAsync("https://xray.example", "cn-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisableUser_ReturnsBadRequest_WhenNotXray()
    {
        var (controller, xrayClient) = CreateController(new VpnServer
        {
            Id = 11,
            ServerName = "ovpn",
            ApiUrl = "https://ovpn.example/",
            ServerType = VpnServerType.OpenVpn
        });

        var result = await controller.DisableUser(
            11,
            new XrayNodeUserActionRequest { CommonName = "cn-1" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        xrayClient.Verify(
            c => c.DisableUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static (VpnServerXrayNodeController Controller, Mock<IXrayNodeApiClient> Client) CreateController(
        VpnServer server)
    {
        var query = new Mock<IVpnServerQueryService>();
        query.Setup(q => q.GetById(server.Id, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        var access = new Mock<IVpnServerAccessQueryService>();
        var client = new Mock<IXrayNodeApiClient>();
        client.Setup(c => c.KickUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        client.Setup(c => c.DisableUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new VpnServerXrayNodeController(
            query.Object,
            client.Object,
            access.Object,
            NullLogger<VpnServerXrayNodeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, "Admin")],
                        "mock"))
                }
            }
        };

        return (controller, client);
    }
}
