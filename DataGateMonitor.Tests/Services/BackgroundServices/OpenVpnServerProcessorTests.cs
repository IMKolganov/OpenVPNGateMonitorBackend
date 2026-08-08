using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class OpenVpnServerProcessorTests
{
    private static (OpenVpnServerProcessor Processor, Mock<IVpnServerService> VpnService, Mock<ICommandService<VpnServer, int>> ServerCmd)
        CreateProcessor()
    {
        var vpnService = new Mock<IVpnServerService>();
        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(x => x.UpdateWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var conflog = new Mock<IVpnServerConflogService>();
        conflog.Setup(x => x.FetchAndSaveIfChangedByServerIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerConflog?)null);

        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(vpnService.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(conflog.Object);
        services.AddSingleton(presence.Object);
        var sp = services.BuildServiceProvider();

        return (new OpenVpnServerProcessor(NullLogger<OpenVpnServerProcessor>.Instance, sp), vpnService, serverCmd);
    }

    [Fact]
    public async Task ProcessServerAsync_OnSuccess_SetsServerOnline()
    {
        var (processor, vpnService, serverCmd) = CreateProcessor();
        vpnService.Setup(x => x.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        vpnService.Setup(x => x.SaveConnectedClientsAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await processor.ProcessServerAsync(OpenVpnHubTestHelpers.OpenVpnServer(), CancellationToken.None);

        serverCmd.Verify(x => x.UpdateWhere(
            It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
            It.IsAny<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<VpnServer>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessServerAsync_OnFailure_SetsServerOfflineAndRethrows()
    {
        var (processor, vpnService, serverCmd) = CreateProcessor();
        vpnService.Setup(x => x.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessServerAsync(OpenVpnHubTestHelpers.OpenVpnServer(), CancellationToken.None));

        serverCmd.Verify(x => x.UpdateWhere(
            It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
            It.IsAny<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<VpnServer>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
