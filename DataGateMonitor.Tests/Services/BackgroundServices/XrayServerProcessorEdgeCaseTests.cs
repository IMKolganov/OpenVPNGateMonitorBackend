using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class XrayServerProcessorEdgeCaseTests
{
    private static ServiceProvider BuildSp(
        Mock<IXrayNodeApiClient> api,
        Mock<IXrayVpnClientSyncService> sync,
        Mock<ICommandService<VpnServer, int>> serverCmd,
        Mock<IVpnServerClientPresenceService> presence,
        Mock<IXrayVpnServerStatusLogService>? statusLog = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(api.Object);
        services.AddSingleton(sync.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton((statusLog ?? new Mock<IXrayVpnServerStatusLogService>(MockBehavior.Loose)).Object);
        return services.BuildServiceProvider();
    }

    private static Mock<ICommandService<VpnServer, int>> ServerCmd()
    {
        var cmd = new Mock<ICommandService<VpnServer, int>>();
        cmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return cmd;
    }

    [Fact]
    public async Task ProcessServerAsync_WhenClientsPayloadNull_MarksOfflineAndDisconnects()
    {
        var api = new Mock<IXrayNodeApiClient>(MockBehavior.Strict);
        api.Setup(a => a.GetActiveClientsAsync("https://x.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync((XrayNodeClientsResponse?)null);

        var sync = new Mock<IXrayVpnClientSyncService>(MockBehavior.Strict);
        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(11, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var serverCmd = ServerCmd();

        var sut = new XrayServerProcessor(
            Mock.Of<ILogger<XrayServerProcessor>>(),
            BuildSp(api, sync, serverCmd, presence));
        var server = new VpnServer
        {
            Id = 11,
            ServerName = "x",
            ApiUrl = "https://x.example/",
            ServerType = VpnServerType.Xray
        };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessServerAsync(server, CancellationToken.None));

        presence.Verify(p => p.MarkAllDisconnectedAsync(11, It.IsAny<CancellationToken>()), Times.Once);
        sync.Verify(
            s => s.SyncConnectedClientsAsync(
                It.IsAny<VpnServer>(),
                It.IsAny<IReadOnlyList<XrayNodeClientDto>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessServerAsync_WhenApiUrlEmpty_MarksOfflineAndDisconnects()
    {
        var api = new Mock<IXrayNodeApiClient>(MockBehavior.Strict);
        var sync = new Mock<IXrayVpnClientSyncService>(MockBehavior.Strict);
        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(12, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var serverCmd = ServerCmd();

        var sut = new XrayServerProcessor(
            Mock.Of<ILogger<XrayServerProcessor>>(),
            BuildSp(api, sync, serverCmd, presence));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessServerAsync(
                new VpnServer { Id = 12, ServerName = "x", ApiUrl = "  ", ServerType = VpnServerType.Xray },
                CancellationToken.None));

        presence.Verify(p => p.MarkAllDisconnectedAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(
            a => a.GetActiveClientsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessServerAsync_OnSuccess_DoesNotClearPresence()
    {
        var payload = new XrayNodeClientsResponse { Clients = [] };
        var api = new Mock<IXrayNodeApiClient>(MockBehavior.Strict);
        api.Setup(a => a.GetActiveClientsAsync("https://x.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var sync = new Mock<IXrayVpnClientSyncService>(MockBehavior.Strict);
        sync.Setup(s => s.SyncConnectedClientsAsync(
                It.IsAny<VpnServer>(),
                payload.Clients,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var presence = new Mock<IVpnServerClientPresenceService>(MockBehavior.Strict);
        var serverCmd = ServerCmd();
        var statusLog = new Mock<IXrayVpnServerStatusLogService>();
        statusLog.Setup(s => s.TryAppendOrUpdateAsync(
                It.IsAny<VpnServer>(),
                payload,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new XrayServerProcessor(
            Mock.Of<ILogger<XrayServerProcessor>>(),
            BuildSp(api, sync, serverCmd, presence, statusLog));

        await sut.ProcessServerAsync(
            new VpnServer { Id = 11, ServerName = "x", ApiUrl = "https://x.example/", ServerType = VpnServerType.Xray },
            CancellationToken.None);

        sync.Verify(s => s.SyncConnectedClientsAsync(
            It.IsAny<VpnServer>(), payload.Clients, It.IsAny<CancellationToken>()), Times.Once);
        presence.Verify(
            p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
