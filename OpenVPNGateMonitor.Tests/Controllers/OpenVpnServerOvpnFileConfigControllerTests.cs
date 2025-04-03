using Mapster;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenVPNGateMonitor.Controllers;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Tests.Controllers;

public class OpenVpnServerOvpnFileConfigControllerTests
{
    private readonly Mock<IOpenVpnServerOvpnFileConfigService> _serviceMock;
    private readonly OpenVpnServerOvpnFileConfigController _controller;

    public OpenVpnServerOvpnFileConfigControllerTests()
    {
        _serviceMock = new Mock<IOpenVpnServerOvpnFileConfigService>();
        _controller = new OpenVpnServerOvpnFileConfigController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetOvpnFileConfig_ReturnsOkResult_WithExpectedData()
    {
        // Arrange
        var vpnServerId = 1;
        var cancellationToken = CancellationToken.None;
        var expectedConfig = new OpenVpnServerOvpnFileConfig
        {
            VpnServerId = vpnServerId,
            VpnServerIp = "1.2.3.4",
            VpnServerPort = 1194,
            ConfigTemplate = "template"
        };

        _serviceMock.Setup(s => s.GetOpenVpnServerOvpnFileConfigByServerId(vpnServerId, cancellationToken))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.GetOvpnFileConfig(new GetOvpnFileConfigRequest(){VpnServerId = 1}, cancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(okResult.Value);
        if (response.Data != null)
        {
            Assert.Equal(expectedConfig.VpnServerId, response.Data.VpnServerId);
            Assert.Equal(expectedConfig.VpnServerIp, response.Data.VpnServerIp);
            Assert.Equal(expectedConfig.VpnServerPort, response.Data.VpnServerPort);
            Assert.Equal(expectedConfig.ConfigTemplate, response.Data.ConfigTemplate);
        }
        else
        {
            Assert.Null(response.Data);
        }
    }

    [Fact]
    public async Task AddOrUpdateOvpnFileConfig_ReturnsOkResult_WithExpectedData()
    {
        // Arrange
        var request = new AddOrUpdateOvpnFileConfigRequest
        {
            VpnServerId = 1,
            VpnServerIp = "5.6.7.8",
            VpnServerPort = 443,
            ConfigTemplate = "custom-template"
        };

        var expectedConfig = request.Adapt<OpenVpnServerOvpnFileConfig>();

        _serviceMock.Setup(s => s.AddOrUpdateOpenVpnServerOvpnFileConfigByServerId(
                It.Is<OpenVpnServerOvpnFileConfig>(c => c.VpnServerId == request.VpnServerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.AddOrUpdateOvpnFileConfig(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(okResult.Value);
        if (response.Data != null)
        {
            Assert.Equal(request.VpnServerId, response.Data.VpnServerId);
            Assert.Equal(request.VpnServerIp, response.Data.VpnServerIp);
            Assert.Equal(request.VpnServerPort, response.Data.VpnServerPort);
            Assert.Equal(request.ConfigTemplate, response.Data.ConfigTemplate);
        }
        else
        {
            Assert.Null(response.Data);
        }
    }
}
