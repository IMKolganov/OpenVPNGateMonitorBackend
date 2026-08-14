using DataGateMonitor.SharedModels.Enums;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnServerOpenVpnProcessServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WhenServerMissing_Throws()
    {
        var query = new Mock<DataGateMonitor.DataBase.Services.Query.VpnServerTable.IVpnServerQueryService>();
        query.Setup(q => q.GetById(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataGateMonitor.Models.VpnServer?)null);

        var sut = new DataGateMonitor.Services.Api.VpnServerOpenVpnProcessService(
            query.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<DataGateMonitor.Services.Api.Auth.Registers.Interfaces.IMicroserviceTokenService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DataGateMonitor.Services.Api.VpnServerOpenVpnProcessService>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetStatusAsync(9, CancellationToken.None));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_WhenNotOpenVpn_Throws()
    {
        var query = new Mock<DataGateMonitor.DataBase.Services.Query.VpnServerTable.IVpnServerQueryService>();
        query.Setup(q => q.GetById(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataGateMonitor.Models.VpnServer
            {
                Id = 3,
                ServerType = VpnServerType.Xray,
                ApiUrl = "https://x.example/"
            });

        var sut = new DataGateMonitor.Services.Api.VpnServerOpenVpnProcessService(
            query.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<DataGateMonitor.Services.Api.Auth.Registers.Interfaces.IMicroserviceTokenService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DataGateMonitor.Services.Api.VpnServerOpenVpnProcessService>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartAsync(3, CancellationToken.None));
        Assert.Contains("OpenVPN", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
