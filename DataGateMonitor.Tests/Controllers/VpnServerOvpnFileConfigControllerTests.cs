using FluentAssertions;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerOvpnFileConfig.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerOvpnFileConfig.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers;

public class VpnServerOvpnFileConfigControllerTests
{
    private readonly Mock<IVpnServerOvpnFileConfigService> _serviceMock;
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGenerationServiceMock;
    private readonly VpnServerOvpnFileConfigController _controller;

    public VpnServerOvpnFileConfigControllerTests()
    {
        _serviceMock = new Mock<IVpnServerOvpnFileConfigService>();
        _statusCacheGenerationServiceMock = new Mock<IStatusCacheGenerationService>();
        _controller = new VpnServerOvpnFileConfigController(_serviceMock.Object, _statusCacheGenerationServiceMock.Object);
    }

    [Fact]
    public async Task GetOvpnFileConfig_ReturnsOkResult_WithExpectedData()
    {
        // Arrange
        var vpnServerId = 1;
        var cancellationToken = CancellationToken.None;
        var expectedConfig = new VpnServerOvpnFileConfig
        {
            VpnServerId = vpnServerId,
            VpnServerIp = "1.2.3.4",
            VpnServerPort = 1194,
            ConfigTemplate = "template"
        };

        _serviceMock.Setup(s => s.GetVpnServerOvpnFileConfigByServerId(vpnServerId, cancellationToken))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.GetOvpnFileConfig(new GetOvpnFileConfigRequest { VpnServerId = vpnServerId }, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(okResult.Value);
        Assert.NotNull(response.Data);

        var data = response.Data!;
        Assert.Equal(expectedConfig.VpnServerId, data.VpnServerId);
        Assert.Equal(expectedConfig.VpnServerIp, data.VpnServerIp);
        Assert.Equal(expectedConfig.VpnServerPort, data.VpnServerPort);
        Assert.Equal(expectedConfig.ConfigTemplate, data.ConfigTemplate);
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
            ConfigTemplate = "custom-template",
            AutoDetectServerSettings = false
        };

        var expectedConfig = request.Adapt<VpnServerOvpnFileConfig>();

        _serviceMock.Setup(s => s.AddOrUpdateVpnServerOvpnFileConfigByServerId(
                It.Is<VpnServerOvpnFileConfig>(c =>
                    c.VpnServerId == request.VpnServerId &&
                    c.VpnServerIp == request.VpnServerIp &&
                    c.VpnServerPort == request.VpnServerPort &&
                    c.ConfigTemplate == request.ConfigTemplate),
                request.AutoDetectServerSettings,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _controller.AddOrUpdateOvpnFileConfig(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(okResult.Value);
        response.Should().NotBeNull();
        response.Data.Should().NotBeNull();

        var data = response.Data!;
        Assert.Equal(request.VpnServerId, data.VpnServerId);
        Assert.Equal(request.VpnServerIp, data.VpnServerIp);
        Assert.Equal(request.VpnServerPort, data.VpnServerPort);
        Assert.Equal(request.ConfigTemplate, data.ConfigTemplate);

        _statusCacheGenerationServiceMock.Verify(s => s.Bump(), Times.Once);
        _serviceMock.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddOrUpdateOvpnFileConfig_ReturnsBadRequest_WhenVpnServerIdIsNotPositive(int vpnServerId)
    {
        // Arrange
        var request = new AddOrUpdateOvpnFileConfigRequest
        {
            VpnServerId = vpnServerId,
            VpnServerIp = "5.6.7.8",
            VpnServerPort = 443,
            ConfigTemplate = "custom-template"
        };

        // Act
        var result = await _controller.AddOrUpdateOvpnFileConfig(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(badRequest.Value);

        Assert.False(response.Success);
        Assert.Equal("VpnServerId must be greater than 0.", response.Message);
        Assert.Null(response.Data);
        _serviceMock.Verify(s => s.AddOrUpdateVpnServerOvpnFileConfigByServerId(
            It.IsAny<VpnServerOvpnFileConfig>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _statusCacheGenerationServiceMock.Verify(s => s.Bump(), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task AddOrUpdateOvpnFileConfig_ReturnsBadRequest_WhenVpnServerIpIsMissing(string? vpnServerIp)
    {
        // Arrange
        var request = new AddOrUpdateOvpnFileConfigRequest
        {
            VpnServerId = 1,
            VpnServerIp = vpnServerIp!,
            VpnServerPort = 443,
            ConfigTemplate = "custom-template"
        };

        // Act
        var result = await _controller.AddOrUpdateOvpnFileConfig(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<OvpnFileConfigResponse>>(badRequest.Value);

        Assert.False(response.Success);
        Assert.Equal("VpnServerIp is required.", response.Message);
        Assert.Null(response.Data);
        _serviceMock.Verify(s => s.AddOrUpdateVpnServerOvpnFileConfigByServerId(
            It.IsAny<VpnServerOvpnFileConfig>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _statusCacheGenerationServiceMock.Verify(s => s.Bump(), Times.Never);
    }
}
