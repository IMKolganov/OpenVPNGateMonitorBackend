using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager;

public class MicroserviceInfoServiceTests
{
    [Fact]
    public async Task GetInfoAsync_WhenServerMissing_Throws()
    {
        var query = new Mock<IVpnServerQueryService>();
        query.Setup(q => q.GetById(9, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServer?)null);

        var sut = new MicroserviceInfoService(
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IMicroserviceTokenService>(),
            query.Object,
            NullLogger<MicroserviceInfoService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetInfoAsync(9, CancellationToken.None));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInfoAsync_WhenApiUrlMissing_Throws()
    {
        var query = new Mock<IVpnServerQueryService>();
        query.Setup(q => q.GetById(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer
            {
                Id = 3,
                ServerName = "x",
                ApiUrl = "  ",
                ServerType = VpnServerType.OpenVpn
            });

        var sut = new MicroserviceInfoService(
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IMicroserviceTokenService>(),
            query.Object,
            NullLogger<MicroserviceInfoService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetInfoAsync(3, CancellationToken.None));
        Assert.Contains("API URL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
