using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStatServiceTests
{
    private readonly Mock<ILogger<IOpenVpnSummaryStatService>> _logger = new();
    private readonly Mock<IOpenVpnClientService> _clients = new();

    private OpenVpnSummaryStatService CreateSut() =>
        new(_logger.Object, _clients.Object);

    private static VpnServer Server() => new() { Id = 42, ServerName = "lt", ApiUrl = "http://127.0.0.1:5012/" };

    [Fact]
    public async Task GetSummaryStatsAsync_AlwaysSumsClientList_WhenDcoEnabled()
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
        Assert.Equal(2, result.ClientsCount);
        Assert.Equal(1300, result.BytesIn);
        Assert.Equal(2040, result.BytesOut);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_AlwaysSumsClientList_WhenDcoDisabled()
    {
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult
            {
                DcoEnabled = false,
                Clients = [new VpnServerClient { BytesReceived = 999, BytesSent = 50 }]
            });

        var result = await CreateSut().GetSummaryStatsAsync(Server(), CancellationToken.None);

        Assert.False(result.DcoEnabled);
        Assert.Equal(1, result.ClientsCount);
        Assert.Equal(999, result.BytesIn);
        Assert.Equal(50, result.BytesOut);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_WhenDcoUnknown_StillSumsClientList()
    {
        _clients.Setup(c => c.GetClientsFromManagementAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult
            {
                DcoEnabled = null,
                Clients = [new VpnServerClient { BytesReceived = 7, BytesSent = 3 }]
            });

        var result = await CreateSut().GetSummaryStatsAsync(Server(), CancellationToken.None);

        Assert.Null(result.DcoEnabled);
        Assert.Equal(7, result.BytesIn);
        Assert.Equal(3, result.BytesOut);
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
        Assert.True(stats.DcoEnabled);
    }

    [Fact]
    public async Task GetSummaryStatsAsync_FollowsDcoToggle_KeepsClientListSource()
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

        var sut = CreateSut();
        var server = Server();

        var on = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.True(on.DcoEnabled);
        Assert.Equal(100, on.BytesIn);
        Assert.Equal(10, on.BytesOut);

        var off = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.False(off.DcoEnabled);
        Assert.Equal(100, off.BytesIn);
        Assert.Equal(10, off.BytesOut);

        var onAgain = await sut.GetSummaryStatsAsync(server, CancellationToken.None);
        Assert.True(onAgain.DcoEnabled);
        Assert.Equal(75, onAgain.BytesIn);
        Assert.Equal(12, onAgain.BytesOut);
    }
}
