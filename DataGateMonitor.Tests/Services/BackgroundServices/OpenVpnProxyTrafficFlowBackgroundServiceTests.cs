using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class OpenVpnProxyTrafficFlowBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StartsListenerWhenServerSupportsTrafficFlow()
    {
        var server = OpenVpnHubTestHelpers.OpenVpnServer();
        var client = new Mock<IOpenVpnProxyTrafficFlowClient>();
        client.Setup(x => x.StartListeningAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var factory = new Mock<IOpenVpnProxyTrafficFlowClientFactory>();
        factory.Setup(x => x.Create(server)).Returns(client.Object);

        var checker = new Mock<IOpenVpnProxyTrafficFlowSupportChecker>();
        checker.Setup(x => x.ShouldListenAsync(server, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var query = new Mock<IVpnServerQueryService>();
        query.Setup(x => x.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>())).ReturnsAsync([server]);

        var services = new ServiceCollection();
        services.AddSingleton(query.Object);
        var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new OpenVpnProxyTrafficFlowBackgroundService(
            NullLogger<OpenVpnProxyTrafficFlowBackgroundService>.Instance,
            factory.Object,
            checker.Object,
            sp.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        factory.Verify(x => x.Create(server), Times.AtLeastOnce);
        client.Verify(x => x.StartListeningAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        factory.Verify(x => x.Remove(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RemovesClientWhenTrafficFlowUnsupported()
    {
        var server = OpenVpnHubTestHelpers.OpenVpnServer();

        var factory = new Mock<IOpenVpnProxyTrafficFlowClientFactory>();
        factory.Setup(x => x.Remove(server.Id)).Returns(true);

        var checker = new Mock<IOpenVpnProxyTrafficFlowSupportChecker>();
        checker.Setup(x => x.ShouldListenAsync(server, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var query = new Mock<IVpnServerQueryService>();
        query.Setup(x => x.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>())).ReturnsAsync([server]);

        var services = new ServiceCollection();
        services.AddSingleton(query.Object);
        var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new OpenVpnProxyTrafficFlowBackgroundService(
            NullLogger<OpenVpnProxyTrafficFlowBackgroundService>.Instance,
            factory.Object,
            checker.Object,
            sp.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        factory.Verify(x => x.Remove(server.Id), Times.AtLeastOnce);
        factory.Verify(x => x.Create(It.IsAny<Models.VpnServer>()), Times.Never);
    }
}
