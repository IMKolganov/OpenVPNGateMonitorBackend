using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStatServiceTests
{
    private readonly Mock<ILogger<IOpenVpnSummaryStatService>> _logger = new();
    private readonly Mock<IOpenVpnMicroserviceClientFactory> _factory = new();
    private readonly Mock<IOpenVpnClientService> _clients = new();
    private readonly Mock<IOpenVpnMicroserviceClient> _mgmt = new();

    private OpenVpnSummaryStatService CreateSut() =>
        new(_logger.Object, _factory.Object, _clients.Object);

    private static VpnServer Server() => new() { Id = 42, ServerName = "lt", ApiUrl = "http://127.0.0.1:5012/" };

    [Fact]
    public async Task GetSummaryStatsAsync_WhenDcoEnabled_SumsClientList_AndSkipsLoadStats()
    {
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult
            {
                DcoEnabled = true,
                Clients =
                [
                    new VpnServerClient { BytesReceived = 1000, BytesSent = 2000 },
                    new VpnServerClient { BytesReceived = 300, BytesSent = 40 }
                ]
            });

        var result = await CreateSut().GetSummaryStatsAsync(Server(), CancellationToken.None);

        Assert.True(result.DcoEnabled);
        Assert.True(result.UsedClientListFallback);
        Assert.Equal(2, result.ClientsCount);
        Assert.Equal(1300, result.BytesIn);
        Assert.Equal(2040, result.BytesOut);
        _factory.Verify(f => f.Create(It.IsAny<VpnServer>()), Times.Never);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_WhenDcoDisabled_UsesLoadStats()
    {
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult
            {
                DcoEnabled = false,
                Clients = [new VpnServerClient { BytesReceived = 999, BytesSent = 999 }]
            });
        _factory.Setup(f => f.Create(It.IsAny<VpnServer>())).Returns(_mgmt.Object);
        _mgmt.Setup(m => m.SendCommandWithResponseAsync("load-stats", It.IsAny<CancellationToken>()))
            .ReturnsAsync("SUCCESS: nclients=3,bytesin=50000,bytesout=60000\n");

        var result = await CreateSut().GetSummaryStatsAsync(Server(), CancellationToken.None);

        Assert.False(result.DcoEnabled);
        Assert.False(result.UsedClientListFallback);
        Assert.Equal(3, result.ClientsCount);
        Assert.Equal(50000, result.BytesIn);
        Assert.Equal(60000, result.BytesOut);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_WhenDcoUnknown_UsesLoadStats()
    {
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult { DcoEnabled = null, Clients = [] });
        _factory.Setup(f => f.Create(It.IsAny<VpnServer>())).Returns(_mgmt.Object);
        _mgmt.Setup(m => m.SendCommandWithResponseAsync("load-stats", It.IsAny<CancellationToken>()))
            .ReturnsAsync("SUCCESS: nclients=0,bytesin=1,bytesout=2\n");

        var result = await CreateSut().GetSummaryStatsAsync(Server(), CancellationToken.None);

        Assert.Null(result.DcoEnabled);
        Assert.False(result.UsedClientListFallback);
        Assert.Equal(1, result.BytesIn);
        Assert.Equal(2, result.BytesOut);
    }

    [Fact]
    public void FromClientList_SumsReceivedAsBytesIn_SentAsBytesOut()
    {
        var stats = OpenVpnSummaryStatService.FromClientList(new OpenVpnManagementStatusResult
        {
            DcoEnabled = true,
            Clients =
            [
                new VpnServerClient { BytesReceived = 10, BytesSent = 1 },
                new VpnServerClient { BytesReceived = 5, BytesSent = 2 }
            ]
        });

        Assert.Equal(15, stats.BytesIn);
        Assert.Equal(3, stats.BytesOut);
        Assert.Equal(2, stats.ClientsCount);
        Assert.True(stats.UsedClientListFallback);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_FollowsDcoToggle_OnThenOffThenOn()
    {
        var call = 0;
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns((VpnServer _, CancellationToken _) =>
            {
                call++;
                var status = call switch
                {
                    1 => new OpenVpnManagementStatusResult
                    {
                        DcoEnabled = true,
                        Clients = [new VpnServerClient { BytesReceived = 100, BytesSent = 10 }]
                    },
                    2 => new OpenVpnManagementStatusResult
                    {
                        DcoEnabled = false,
                        Clients = [new VpnServerClient { BytesReceived = 100, BytesSent = 10 }]
                    },
                    _ => new OpenVpnManagementStatusResult
                    {
                        DcoEnabled = true,
                        Clients =
                        [
                            new VpnServerClient { BytesReceived = 50, BytesSent = 5 },
                            new VpnServerClient { BytesReceived = 25, BytesSent = 7 }
                        ]
                    }
                };
                return Task.FromResult(status);
            });
        _factory.Setup(f => f.Create(It.IsAny<VpnServer>())).Returns(_mgmt.Object);
        _mgmt.Setup(m => m.SendCommandWithResponseAsync("load-stats", It.IsAny<CancellationToken>()))
            .ReturnsAsync("SUCCESS: nclients=1,bytesin=9000,bytesout=8000\n");

        var sut = CreateSut();
        var server = Server();

        var on = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.True(on.DcoEnabled);
        Assert.True(on.UsedClientListFallback);
        Assert.Equal(100, on.BytesIn);
        Assert.Equal(10, on.BytesOut);
        _factory.Verify(f => f.Create(It.IsAny<VpnServer>()), Times.Never);

        var off = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.False(off.DcoEnabled);
        Assert.False(off.UsedClientListFallback);
        Assert.Equal(9000, off.BytesIn);
        Assert.Equal(8000, off.BytesOut);
        _factory.Verify(f => f.Create(It.IsAny<VpnServer>()), Times.Once);
        _mgmt.Verify(m => m.SendCommandWithResponseAsync("load-stats", It.IsAny<CancellationToken>()), Times.Once);

        var onAgain = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.True(onAgain.DcoEnabled);
        Assert.True(onAgain.UsedClientListFallback);
        Assert.Equal(75, onAgain.BytesIn);
        Assert.Equal(12, onAgain.BytesOut);
        // Still only one load-stats call from the middle (off) poll.
        _factory.Verify(f => f.Create(It.IsAny<VpnServer>()), Times.Once);
        _mgmt.Verify(m => m.SendCommandWithResponseAsync("load-stats", It.IsAny<CancellationToken>()), Times.Once);
    }
}
