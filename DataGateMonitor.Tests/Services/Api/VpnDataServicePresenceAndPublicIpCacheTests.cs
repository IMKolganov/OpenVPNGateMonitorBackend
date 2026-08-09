using DataGateMonitor.Models;
using DataGateMonitor.Tests.Services.Api;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnDataServicePresenceAndPublicIpCacheTests
{
    [Fact]
    public async Task DeleteVpnServer_MarksClientsDisconnected_AndInvalidatesPublicIpCache()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 9, ServerName = "gone" });
        h.ServerCmd.Setup(c => c.UpdateWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(9, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ok = await h.Create().DeleteVpnServer(9, CancellationToken.None);

        Assert.True(ok);
        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(9, It.IsAny<CancellationToken>()), Times.Once);
        h.PublicIpLookup.Verify(p => p.Invalidate(9), Times.Once);
        h.MicroserviceFactory.Verify(f => f.Invalidate(9), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenBecomesDisabled_MarksClientsDisconnected()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(8, "srv8");
        h.ServerQ.SetupSequence(q => q.GetById(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 8, ServerName = "srv8", IsDisable = false, ApiUrl = "https://a/" })
            .ReturnsAsync(new VpnServer { Id = 8, ServerName = "srv8", IsDisable = true, ApiUrl = "https://a/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(8, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 8, ServerName = "srv8", IsDisable = true, ApiUrl = "https://a/" },
            [],
            [],
            CancellationToken.None);

        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(8, It.IsAny<CancellationToken>()), Times.Once);
        h.PublicIpLookup.Verify(p => p.Invalidate(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenApiUrlChanges_InvalidatesPublicIpCache_WithoutDisconnectIfStillEnabled()
    {
        var h = new VpnDataServiceTestHarness();
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

        h.PublicIpLookup.Verify(p => p.Invalidate(5), Times.Once);
        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenBecomesEnabled_DoesNotMarkClientsDisconnected()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(3, "srv3");
        h.ServerQ.SetupSequence(q => q.GetById(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 3, ServerName = "srv3", IsDisable = true, ApiUrl = "https://a/" })
            .ReturnsAsync(new VpnServer { Id = 3, ServerName = "srv3", IsDisable = false, ApiUrl = "https://a/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 3, ServerName = "srv3", IsDisable = false, ApiUrl = "https://a/" },
            [],
            [],
            CancellationToken.None);

        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.PublicIpLookup.Verify(p => p.Invalidate(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenAlreadyDisabled_DoesNotMarkClientsDisconnectedAgain()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(4, "srv4");
        h.ServerQ.SetupSequence(q => q.GetById(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 4, ServerName = "srv4", IsDisable = true, ApiUrl = "https://a/" })
            .ReturnsAsync(new VpnServer { Id = 4, ServerName = "srv4", IsDisable = true, ApiUrl = "https://a/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(4, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 4, ServerName = "srv4", IsDisable = true, ApiUrl = "https://a/" },
            [],
            [],
            CancellationToken.None);

        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenDisableAndApiUrlChange_MarksDisconnected_AndInvalidatesPublicIp()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(6, "srv6");
        h.ServerQ.SetupSequence(q => q.GetById(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 6, ServerName = "srv6", IsDisable = false, ApiUrl = "https://old/" })
            .ReturnsAsync(new VpnServer { Id = 6, ServerName = "srv6", IsDisable = true, ApiUrl = "https://new/" });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(6, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        h.Presence.Setup(p => p.MarkAllDisconnectedAsync(6, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 6, ServerName = "srv6", IsDisable = true, ApiUrl = "https://new/" },
            [],
            [],
            CancellationToken.None);

        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(6, It.IsAny<CancellationToken>()), Times.Once);
        h.PublicIpLookup.Verify(p => p.Invalidate(6), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_WhenApiUrlDiffersOnlyByCaseOrWhitespace_DoesNotInvalidate()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(15, "srv15");
        h.ServerQ.SetupSequence(q => q.GetById(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 15, ServerName = "srv15", IsDisable = false, ApiUrl = "https://A.example/" })
            .ReturnsAsync(new VpnServer { Id = 15, ServerName = "srv15", IsDisable = false, ApiUrl = "  HTTPS://a.example/  " });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(15, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await h.Create().UpdateVpnServer(
            new VpnServer { Id = 15, ServerName = "srv15", IsDisable = false, ApiUrl = "  HTTPS://a.example/  " },
            [],
            [],
            CancellationToken.None);

        h.PublicIpLookup.Verify(p => p.Invalidate(It.IsAny<int>()), Times.Never);
        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteVpnServer_WhenNotFound_Throws_WithoutPresenceOrInvalidate()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Create().DeleteVpnServer(404, CancellationToken.None));

        h.Presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        h.PublicIpLookup.Verify(p => p.Invalidate(It.IsAny<int>()), Times.Never);
    }
}
