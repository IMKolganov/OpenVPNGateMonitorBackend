using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;
using DataGateMonitor.SharedModels.DataGateXRayManager.Info;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.Helpers;

public class VpnNodePublicIpLookupTests
{
    private readonly Mock<IMicroserviceInfoService> _info = new();
    private readonly Mock<ILogger<VpnNodePublicIpLookup>> _logger = new();

    private VpnNodePublicIpLookup CreateSut(IMemoryCache? cache = null) => new(
        _info.Object,
        cache ?? new MemoryCache(new MemoryCacheOptions()),
        _logger.Object);

    [Fact]
    public async Task GetAsync_ReturnsOpenVpnPublicIp_AndCaches()
    {
        var calls = 0;
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new VpnMicroserviceDiagnosticsDto
                {
                    OpenVpn = new RootOpenVpnInfoResponse { PublicIp = "198.51.100.10" }
                };
            });

        var sut = CreateSut();
        Assert.Equal("198.51.100.10", await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Equal("198.51.100.10", await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_ReturnsXrayPublicIp()
    {
        _info.Setup(i => i.GetInfoAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                Xray = new RootXrayInfoResponse { PublicIp = "203.0.113.5" }
            });

        Assert.Equal("203.0.113.5",
            await CreateSut().GetAsync(11, VpnServerType.Xray, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WhenInfoFails_CachesNegative_AndDoesNotRetryImmediately()
    {
        var calls = 0;
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                return Task.FromException<VpnMicroserviceDiagnosticsDto>(new HttpRequestException("down"));
            });

        var sut = CreateSut();
        Assert.Null(await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Null(await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_WhenPublicIpMissing_CachesEmpty()
    {
        var calls = 0;
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new VpnMicroserviceDiagnosticsDto { OpenVpn = new RootOpenVpnInfoResponse() };
            });

        var sut = CreateSut();
        Assert.Null(await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Null(await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Invalidate_ClearsCache_SoNextCallRefetches()
    {
        var calls = 0;
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new VpnMicroserviceDiagnosticsDto
                {
                    OpenVpn = new RootOpenVpnInfoResponse { PublicIp = $"203.0.113.{calls}" }
                };
            });

        var sut = CreateSut();
        Assert.Equal("203.0.113.1", await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        sut.Invalidate(7);
        Assert.Equal("203.0.113.2", await sut.GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetAsync_TrimsPublicIpWhitespace()
    {
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                OpenVpn = new RootOpenVpnInfoResponse { PublicIp = "  203.0.113.8 \n" }
            });

        Assert.Equal("203.0.113.8",
            await CreateSut().GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_OpenVpnLookup_DoesNotReadXrayPublicIp()
    {
        _info.Setup(i => i.GetInfoAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                Xray = new RootXrayInfoResponse { PublicIp = "203.0.113.1" },
                OpenVpn = new RootOpenVpnInfoResponse { PublicIp = null }
            });

        Assert.Null(await CreateSut().GetAsync(7, VpnServerType.OpenVpn, CancellationToken.None));
    }
}
