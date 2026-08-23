using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.PiHoleHealth;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Diagnostics.Responses;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.PiHoleHealth;

public class PiHoleHealthCheckRunnerTests
{
    [Fact]
    public async Task RunAsync_NotifiesRecoveredAfterPriorUnhealthyAlert()
    {
        var server = new VpnServer
        {
            Id = 75,
            ServerName = "Norway",
            IsPiHoleEnabled = true,
            IsDeleted = false,
            IsDisable = false
        };

        var vpnServers = new Mock<IVpnServerQueryService>();
        vpnServers.Setup(x => x.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VpnServer> { server });

        var piHoleConfig = new Mock<IVpnServerPiHoleConfigService>();
        piHoleConfig.SetupSequence(x => x.GetMicroserviceDiagnosticsAsync(75, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiHoleDiagnosticsResponse
            {
                Health = "Disabled",
                HealthMessage = "Collector is disabled on the VPN microservice."
            })
            .ReturnsAsync(new PiHoleDiagnosticsResponse { Health = "Ok" });

        var notifications = new Mock<IPiHoleHealthNotificationService>();
        var tracker = new PiHoleHealthNotificationTracker();
        var sut = new PiHoleHealthCheckRunner(
            Mock.Of<ILogger<PiHoleHealthCheckRunner>>(),
            vpnServers.Object,
            piHoleConfig.Object,
            notifications.Object,
            tracker);

        await sut.RunAsync(CancellationToken.None);
        await sut.RunAsync(CancellationToken.None);

        notifications.Verify(
            x => x.NotifyUnhealthyAsync(
                75,
                "Norway",
                "Disabled",
                "Collector is disabled on the VPN microservice.",
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            x => x.NotifyRecoveredAsync(75, "Norway", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DoesNotNotifyRecoveredWhenServerWasAlwaysHealthy()
    {
        var server = new VpnServer
        {
            Id = 77,
            ServerName = "Norway 2",
            IsPiHoleEnabled = true,
            IsDeleted = false,
            IsDisable = false
        };

        var vpnServers = new Mock<IVpnServerQueryService>();
        vpnServers.Setup(x => x.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VpnServer> { server });

        var piHoleConfig = new Mock<IVpnServerPiHoleConfigService>();
        piHoleConfig.Setup(x => x.GetMicroserviceDiagnosticsAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PiHoleDiagnosticsResponse { Health = "Ok" });

        var notifications = new Mock<IPiHoleHealthNotificationService>();
        var sut = new PiHoleHealthCheckRunner(
            Mock.Of<ILogger<PiHoleHealthCheckRunner>>(),
            vpnServers.Object,
            piHoleConfig.Object,
            notifications.Object,
            new PiHoleHealthNotificationTracker());

        await sut.RunAsync(CancellationToken.None);

        notifications.Verify(
            x => x.NotifyRecoveredAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
