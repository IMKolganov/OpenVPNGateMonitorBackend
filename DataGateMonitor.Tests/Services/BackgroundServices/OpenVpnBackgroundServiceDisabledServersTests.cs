using System.Linq.Expressions;
using System.Reflection;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class OpenVpnBackgroundServiceDisabledServersTests : IDisposable
{
    public OpenVpnBackgroundServiceDisabledServersTests() => ResetInstanceCount();

    public void Dispose() => ResetInstanceCount();

    private static void ResetInstanceCount()
    {
        var field = typeof(OpenVpnBackgroundService).GetField(
            "_instanceCount", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(null, 0);
    }

    private static async Task InvokeRunOpenVpnTaskAsync(OpenVpnBackgroundService sut, int nextRunSeconds)
    {
        var method = typeof(OpenVpnBackgroundService).GetMethod(
            "RunOpenVpnTask", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(sut, [nextRunSeconds, CancellationToken.None])!;
        await task;
    }

    private static (OpenVpnBackgroundService Sut, Mock<IVpnServerClientPresenceService> Presence, Mock<IVpnServerService> VpnService, VpnServerStatusManager Status)
        CreateSut(List<VpnServer> servers, MockBehavior presenceBehavior = MockBehavior.Loose)
    {
        var serverQ = new Mock<IVpnServerQueryService>();
        serverQ.Setup(q => q.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

        var presence = new Mock<IVpnServerClientPresenceService>(presenceBehavior);
        if (presenceBehavior != MockBehavior.Strict)
        {
            presence.Setup(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        var vpnService = new Mock<IVpnServerService>();
        vpnService.Setup(s => s.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        vpnService.Setup(s => s.SaveConnectedClientsAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var conflog = new Mock<IVpnServerConflogService>();
        conflog.Setup(c => c.FetchAndSaveIfChangedByServerIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerConflog?)null);

        var services = new ServiceCollection();
        services.AddSingleton(serverQ.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton(vpnService.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(conflog.Object);
        services.AddSingleton(Mock.Of<IServerOpenVpnNotificationService>());
        services.AddSingleton(Mock.Of<ILogger<OpenVpnServerProcessor>>());
        services.AddSingleton(Mock.Of<ILogger<XrayServerProcessor>>());
        var sp = services.BuildServiceProvider();

        var logStore = new Mock<IStatusStreamLogStore>();
        logStore.Setup(s => s.AppendAsync(It.IsAny<StatusStreamLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenVpnPolling:MaxDegreeOfParallelism"] = "1"
        }).Build();

        var statusManager = new VpnServerStatusManager();
        var sut = new OpenVpnBackgroundService(
            Mock.Of<ILogger<OpenVpnBackgroundService>>(),
            sp,
            new VpnServerProcessorFactory(sp),
            statusManager,
            Mock.Of<IStatusCacheGenerationService>(),
            logStore.Object,
            config);

        return (sut, presence, vpnService, statusManager);
    }

    [Fact]
    public async Task RunOpenVpnTask_MarksDisabledServersDisconnected_AndDoesNotPollThem()
    {
        var enabled = new VpnServer
        {
            Id = 1,
            ServerName = "on",
            ApiUrl = "https://on.example/",
            IsDisable = false,
            ServerType = VpnServerType.OpenVpn
        };
        var disabledA = new VpnServer
        {
            Id = 2,
            ServerName = "off-a",
            ApiUrl = "https://a.example/",
            IsDisable = true,
            ServerType = VpnServerType.OpenVpn
        };
        var disabledB = new VpnServer
        {
            Id = 3,
            ServerName = "off-b",
            ApiUrl = "https://b.example/",
            IsDisable = true,
            ServerType = VpnServerType.Xray
        };

        var (sut, presence, vpnService, status) = CreateSut([enabled, disabledA, disabledB]);
        await InvokeRunOpenVpnTaskAsync(sut, nextRunSeconds: 30);

        presence.Verify(p => p.MarkAllDisconnectedAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        presence.Verify(p => p.MarkAllDisconnectedAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        presence.Verify(p => p.MarkAllDisconnectedAsync(1, It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(ServiceStatus.Idle, status.GetStatus(2).Status);
        Assert.Equal(ServiceStatus.Idle, status.GetStatus(3).Status);

        vpnService.Verify(
            s => s.SaveConnectedClientsAsync(It.Is<VpnServer>(v => v.Id == 1), It.IsAny<CancellationToken>()),
            Times.Once);
        vpnService.Verify(
            s => s.SaveConnectedClientsAsync(It.Is<VpnServer>(v => v.Id == 2 || v.Id == 3), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunOpenVpnTask_WhenOnlyEnabled_DoesNotCallPresenceFromDisableLoop()
    {
        var enabled = new VpnServer
        {
            Id = 5,
            ServerName = "on",
            ApiUrl = "https://on.example/",
            IsDisable = false,
            ServerType = VpnServerType.OpenVpn
        };

        var (sut, presence, _, _) = CreateSut([enabled], MockBehavior.Strict);
        await InvokeRunOpenVpnTaskAsync(sut, 30);

        presence.Verify(
            p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
