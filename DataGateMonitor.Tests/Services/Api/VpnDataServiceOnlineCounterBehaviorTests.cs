using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

/// <summary>
/// VpnDataService disable/delete must drive presence so the per-server Redis online counter drops to 0.
/// </summary>
public class VpnDataServiceOnlineCounterBehaviorTests
{
    [Fact]
    public async Task DeleteVpnServer_WithConnectedClients_ZerosOnlineCounter()
    {
        var counts = new Dictionary<int, int> { [9] = 4 };
        var counter = new Mock<IConnectedClientsCounterStore>(MockBehavior.Strict);
        counter
            .Setup(c => c.SetAsync(9, 0, It.IsAny<CancellationToken>()))
            .Callback<int, int, CancellationToken>((id, n, _) => counts[id] = n)
            .Returns(Task.CompletedTask);

        var clientCmd = new Mock<ICommandService<VpnServerClient, int>>();
        clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var presence = new VpnServerClientPresenceService(
            clientCmd.Object,
            counter.Object,
            Mock.Of<ILogger<VpnServerClientPresenceService>>());

        var h = new VpnDataServiceTestHarness();
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(9, It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>((id, ct) => presence.MarkAllDisconnectedAsync(id, ct));

        h.ServerQ.Setup(q => q.GetById(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 9, ServerName = "gone" });
        h.ServerCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var ok = await h.Create().DeleteVpnServer(9, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, counts[9]);
        counter.Verify(c => c.SetAsync(9, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenDisabled_WithConnectedClients_ZerosOnlineCounter()
    {
        var counts = new Dictionary<int, int> { [8] = 3 };
        var counter = new Mock<IConnectedClientsCounterStore>(MockBehavior.Strict);
        counter
            .Setup(c => c.SetAsync(8, 0, It.IsAny<CancellationToken>()))
            .Callback<int, int, CancellationToken>((id, n, _) => counts[id] = n)
            .Returns(Task.CompletedTask);

        var clientCmd = new Mock<ICommandService<VpnServerClient, int>>();
        clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var presence = new VpnServerClientPresenceService(
            clientCmd.Object,
            counter.Object,
            Mock.Of<ILogger<VpnServerClientPresenceService>>());

        var h = new VpnDataServiceTestHarness();
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(8, It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>((id, ct) => presence.MarkAllDisconnectedAsync(id, ct));

        h.SetupUpdateServer(8, "srv8");
        h.ServerQ.SetupSequence(q => q.GetById(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 8, ServerName = "srv8", IsDisable = false, ApiUrl = "https://a/" })
            .ReturnsAsync(new VpnServer { Id = 8, ServerName = "srv8", IsDisable = true, ApiUrl = "https://a/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 8, ServerName = "srv8", IsDisable = true, ApiUrl = "https://a/" },
            [],
            [],
            CancellationToken.None);

        Assert.Equal(0, counts[8]);
        counter.Verify(c => c.SetAsync(8, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenOnlyApiUrlChanges_LeavesOnlineCounterUntouched()
    {
        var counter = new Mock<IConnectedClientsCounterStore>(MockBehavior.Strict);
        var clientCmd = new Mock<ICommandService<VpnServerClient, int>>(MockBehavior.Strict);
        var presence = new VpnServerClientPresenceService(
            clientCmd.Object,
            counter.Object,
            Mock.Of<ILogger<VpnServerClientPresenceService>>());

        var h = new VpnDataServiceTestHarness();
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<int, CancellationToken>((id, ct) => presence.MarkAllDisconnectedAsync(id, ct));

        h.SetupUpdateServer(5, "srv5");
        h.ServerQ.SetupSequence(q => q.GetById(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 5, ServerName = "srv5", IsDisable = false, ApiUrl = "https://old/" })
            .ReturnsAsync(new VpnServer { Id = 5, ServerName = "srv5", IsDisable = false, ApiUrl = "https://new/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 5, ServerName = "srv5", IsDisable = false, ApiUrl = "https://new/" },
            [],
            [],
            CancellationToken.None);

        counter.Verify(c => c.SetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        clientCmd.Verify(
            c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        h.PublicIpLookup.Verify(p => p.Invalidate(5), Times.Once);
    }
}
