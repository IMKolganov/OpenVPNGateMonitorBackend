using Moq;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

namespace DataGateMonitor.Tests.Services.Cache;

public class VpnServerConnectedCountOverlayTests
{
    [Fact]
    public async Task ApplyAsync_V1_Overwrites_Connected_Counts_From_Redis()
    {
        var store = new Mock<IConnectedClientsCounterStore>(MockBehavior.Strict);
        store.Setup(s => s.GetManyAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [5] = 42, [7] = 0 });

        var items = new List<VpnServerWithStatusDto>
        {
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 5, ServerName = "a" }
                },
                CountConnectedClients = 1
            },
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 7, ServerName = "b" }
                },
                CountConnectedClients = 9
            },
            new()
            {
                VpnServerResponses = new VpnServerResponse
                {
                    VpnServer = new VpnServerDto { Id = 9, ServerName = "c" }
                },
                CountConnectedClients = 3
            }
        };

        await VpnServerConnectedCountOverlay.ApplyAsync(items, store.Object);

        Assert.Equal(42, items[0].CountConnectedClients);
        Assert.Equal(0, items[1].CountConnectedClients);
        Assert.Equal(3, items[2].CountConnectedClients); // unchanged when Redis miss
        store.VerifyAll();
    }

    [Fact]
    public async Task ApplyAsync_Empty_Or_Null_Map_Is_NoOp()
    {
        var store = new Mock<IConnectedClientsCounterStore>();
        store.Setup(s => s.GetManyAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<int, int>?)null!);

        var items = new List<VpnServerWithStatusV2Dto>
        {
            new()
            {
                VpnServerResponses = new VpnServerV2Response
                {
                    VpnServer = new VpnServerV2Dto { Id = 1, ServerName = "x" }
                },
                CountConnectedClients = 5
            }
        };

        await VpnServerConnectedCountOverlay.ApplyAsync(items, store.Object);
        Assert.Equal(5, items[0].CountConnectedClients);
    }
}
