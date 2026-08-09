using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.Models.XrayNode;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class VpnServerProcessorPresenceOnFailureTests
{
    [Fact]
    public async Task OpenVpnServerProcessor_OnFailure_MarksServerOffline_AndDisconnectsClients()
    {
        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var vpnServerService = new Mock<IVpnServerService>();
        vpnServerService
            .Setup(s => s.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("node down"));

        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(vpnServerService.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton(Mock.Of<IVpnServerConflogService>());
        var sp = services.BuildServiceProvider();

        var sut = new OpenVpnServerProcessor(Mock.Of<ILogger<OpenVpnServerProcessor>>(), sp);
        var server = new VpnServer { Id = 7, ServerName = "s", ApiUrl = "https://s.example/" };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessServerAsync(server, CancellationToken.None));

        presence.Verify(p => p.MarkAllDisconnectedAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        serverCmd.Verify(c => c.UpdateWhere(
            It.IsAny<Expression<Func<VpnServer, bool>>>(),
            It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task XrayServerProcessor_OnFailure_MarksServerOffline_AndDisconnectsClients()
    {
        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var xrayApi = new Mock<IXrayNodeApiClient>();
        xrayApi.Setup(a => a.GetActiveClientsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("xray down"));

        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(11, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(xrayApi.Object);
        services.AddSingleton(Mock.Of<IXrayVpnClientSyncService>());
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton(Mock.Of<IXrayVpnServerStatusLogService>());
        var sp = services.BuildServiceProvider();

        var sut = new XrayServerProcessor(Mock.Of<ILogger<XrayServerProcessor>>(), sp);
        var server = new VpnServer { Id = 11, ServerName = "x", ApiUrl = "https://x.example/" };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessServerAsync(server, CancellationToken.None));

        presence.Verify(p => p.MarkAllDisconnectedAsync(11, It.IsAny<CancellationToken>()), Times.Once);
    }
}
