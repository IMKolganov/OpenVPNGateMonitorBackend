using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class XrayDnsEventBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_StartsListening_OnlyForEnabledXrayServers()
    {
        var xray = new VpnServer
        {
            Id = 1,
            ServerName = "xray-a",
            ApiUrl = "https://xs1.datagateapp.com/",
            ServerType = VpnServerType.Xray,
            IsDisable = false
        };
        var disabledXray = new VpnServer
        {
            Id = 2,
            ServerName = "xray-off",
            ApiUrl = "https://xs2.datagateapp.com/",
            ServerType = VpnServerType.Xray,
            IsDisable = true
        };
        var openVpn = OpenVpnHubTestHelpers.OpenVpnServer(id: 3);

        StubXrayDnsEventClient? stubClient = null;
        var factory = new Mock<IXrayDnsEventClientFactory>();
        factory.Setup(x => x.Create(It.IsAny<VpnServer>()))
            .Returns((VpnServer server) =>
            {
                stubClient ??= new StubXrayDnsEventClient(server);
                return stubClient;
            });

        var query = new Mock<IVpnServerQueryService>();
        query.Setup(x => x.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([xray, disabledXray, openVpn]);

        var services = new ServiceCollection();
        services.AddSingleton(query.Object);
        var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sut = new XrayDnsEventBackgroundService(
            NullLogger<XrayDnsEventBackgroundService>.Instance,
            factory.Object,
            sp.GetRequiredService<IServiceScopeFactory>());

        await sut.StartAsync(cts.Token);
        await Task.Delay(250, CancellationToken.None);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        factory.Verify(x => x.Create(It.Is<VpnServer>(s => s.Id == 1)), Times.AtLeastOnce);
        factory.Verify(x => x.Create(It.Is<VpnServer>(s => s.Id == 2)), Times.Never);
        factory.Verify(x => x.Create(It.Is<VpnServer>(s => s.Id == 3)), Times.Never);
        Assert.NotNull(stubClient);
        Assert.True(stubClient!.StartListeningCallCount >= 1);
    }

    private sealed class StubXrayDnsEventClient(VpnServer server) : XrayDnsEventClient(
        server,
        NullLogger<XrayDnsEventClient>.Instance,
        OpenVpnHubTestHelpers.CreateTokenService(),
        Mock.Of<IServiceScopeFactory>())
    {
        public int StartListeningCallCount { get; private set; }

        public override Task StartListeningAsync(CancellationToken cancellationToken)
        {
            StartListeningCallCount++;
            return Task.CompletedTask;
        }
    }
}
