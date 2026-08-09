using System.Security.Claims;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.DataGateXRayManager.ClientLinks;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Responses;
using DataGateMonitor.SharedModels.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Controllers;

public class XrayClientLinksControllerTests
{
    private readonly Mock<IXrayClientLinkService> _service = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IQuotaPlanAllowedServerQueryService> _quotaAllowed = new();
    private readonly Mock<IVpnServerAccessQueryService> _vpnAccess = new();
    private readonly XrayClientLinksController _controller;

    public XrayClientLinksControllerTests()
    {
        _controller = new XrayClientLinksController(
            _service.Object,
            NullLogger<XrayClientLinksController>.Instance,
            _userQuotaPlan.Object,
            _quotaAllowed.Object,
            _vpnAccess.Object)
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
    }

    [Fact]
    public async Task GetByToken_ReturnsBadRequest_WhenTokenEmpty()
    {
        var result = await _controller.GetByToken(new ByTokenRequest { Token = "  " }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(Assert.IsType<ApiResponse<OvpnFileResponse>>(bad.Value).Success);
    }

    [Fact]
    public async Task GetAllByVpnServerId_ReturnsOk()
    {
        _service.Setup(s => s.GetAllByVpnServerId(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IssuedXrayClientLink>());

        var result = await _controller.GetAllByVpnServerId(
            new ByVpnServerIdRequest { VpnServerId = 5 },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<OvpnFilesResponse>>(ok.Value).Success);
    }

    [Fact]
    public async Task GetByToken_ReturnsOk()
    {
        _service.Setup(s => s.GetByToken("tkn", It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new IssuedXrayClientLink { VpnServerId = 5, CommonName = "cn" });

        var result = await _controller.GetByToken(new ByTokenRequest { Token = "tkn" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(Assert.IsType<ApiResponse<OvpnFileResponse>>(ok.Value).Success);
    }

    [Fact]
    public async Task AddFile_ReturnsBadRequest_OnException()
    {
        _service.Setup(s => s.AddClientLink(It.IsAny<AddFileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _controller.AddFile(
            new AddFileRequest { CommonName = "cn", VpnServerId = 1 },
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(Assert.IsType<ApiResponse<OvpnFileResponse>>(bad.Value).Success);
    }
}
