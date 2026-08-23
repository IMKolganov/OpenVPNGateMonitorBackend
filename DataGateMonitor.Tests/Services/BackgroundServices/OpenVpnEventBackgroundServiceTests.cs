using System.Reflection;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Tests.Helpers;
using DataGateMonitor.Tests.Services.DataGateOpenVpnManager.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

[Collection("OpenVpnBackgroundServiceSingleton")]
public class OpenVpnEventBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesEventClientForEachEnabledServer()
    {
        ResetInstanceCount();

        var server = OpenVpnHubTestHelpers.OpenVpnServer();
        StubOpenVpnEventClient? stubClient = null;
        var factory = new Mock<IOpenVpnEventClientFactory>();
        factory.Setup(x => x.GetAllClients()).Returns(Array.Empty<OpenVpnEventClient>());
        factory.Setup(x => x.Create(server)).Returns(() =>
        {
            stubClient ??= new StubOpenVpnEventClient(
                server,
                NullLogger<OpenVpnEventClient>.Instance,
                Mock.Of<IHubContext<OpenVpnEventHub>>(),
                OpenVpnHubTestHelpers.CreateTokenService(),
                Mock.Of<IServiceScopeFactory>());
            return stubClient;
        });

        var query = new Mock<IVpnServerQueryService>();
        query.Setup(x => x.GetAll(false, false, null, It.IsAny<CancellationToken>())).ReturnsAsync([server]);

        var services = new ServiceCollection();
        services.AddSingleton(query.Object);
        var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new OpenVpnEventBackgroundService(
            NullLogger<OpenVpnEventBackgroundService>.Instance,
            factory.Object,
            sp.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        factory.Verify(x => x.Create(server), Times.AtLeastOnce);
        Assert.NotNull(stubClient);
        Assert.True(stubClient!.StartListeningCallCount >= 1);

        ResetInstanceCount();
    }

    [Fact]
    public async Task ExecuteAsync_PrunesCachedClient_WhenOpenVpnServerSoftDeleted()
    {
        ResetInstanceCount();

        // Simulates mis-typed OpenVPN → xs1-pol:9443 that was deleted from DB but still cached.
        var orphan = OpenVpnHubTestHelpers.OpenVpnServer(
            id: 91,
            apiUrl: "https://xs1-pol.datagateapp.com:9443/");
        var orphanClient = new StubOpenVpnEventClient(
            orphan,
            NullLogger<OpenVpnEventClient>.Instance,
            Mock.Of<IHubContext<OpenVpnEventHub>>(),
            OpenVpnHubTestHelpers.CreateTokenService(),
            Mock.Of<IServiceScopeFactory>());

        var factory = new Mock<IOpenVpnEventClientFactory>();
        factory.Setup(x => x.GetAllClients()).Returns([orphanClient]);
        factory.Setup(x => x.Remove(91)).Returns(true);

        var query = new Mock<IVpnServerQueryService>();
        query.Setup(x => x.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // soft-deleted / gone from active list

        var services = new ServiceCollection();
        services.AddSingleton(query.Object);
        var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new OpenVpnEventBackgroundService(
            NullLogger<OpenVpnEventBackgroundService>.Instance,
            factory.Object,
            sp.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(cts.Token);
        await Task.Delay(250, CancellationToken.None);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        factory.Verify(x => x.Remove(91), Times.AtLeastOnce);
        factory.Verify(x => x.Create(It.IsAny<DataGateMonitor.Models.VpnServer>()), Times.Never);

        ResetInstanceCount();
    }

    private static void ResetInstanceCount()
    {
        var field = typeof(OpenVpnEventBackgroundService).GetField("_instanceCount", BindingFlags.Static | BindingFlags.NonPublic);
        field!.SetValue(null, 0);
    }
}

[CollectionDefinition("OpenVpnBackgroundServiceSingleton", DisableParallelization = true)]
public sealed class OpenVpnBackgroundServiceSingletonCollection;
