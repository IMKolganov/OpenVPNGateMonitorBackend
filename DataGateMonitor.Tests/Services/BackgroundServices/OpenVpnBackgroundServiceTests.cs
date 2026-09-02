using System.Reflection;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class OpenVpnBackgroundServiceTests
{
    [Fact]
    public async Task RunOpenVpnTask_AppendsOperationalEvents_ForSuccessfulServerPolling()
    {
        ResetOpenVpnBackgroundServiceInstanceCount();
        var server = new VpnServer
        {
            Id = 1,
            ServerName = "test-node-1",
            ApiUrl = "http://node-1",
            ServerType = VpnServerType.OpenVpn,
            IsDisable = false
        };

        var queryService = new Mock<IVpnServerQueryService>();
        queryService.Setup(x => x.GetAll(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),It.IsAny<UserVpnServerAccessOverrides?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([server]);

        var notificationService = new Mock<IServerOpenVpnNotificationService>();
        var presence = new Mock<IVpnServerClientPresenceService>(MockBehavior.Strict);
        var serviceProvider = BuildServiceProvider(
            queryService.Object,
            notificationService.Object,
            presence.Object);

        var factory = new VpnServerProcessorFactory(serviceProvider);
        InjectProcessor(factory, server.Id, server.ServerType, new FakeProcessor());

        var logStore = new Mock<IStatusStreamLogStore>();
        var entries = new List<StatusStreamLogEntry>();
        logStore.Setup(x => x.AppendAsync(It.IsAny<StatusStreamLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<StatusStreamLogEntry, CancellationToken>((entry, _) => entries.Add(entry));

        var cacheGeneration = new Mock<IStatusCacheGenerationService>();
        cacheGeneration.Setup(x => x.Bump()).Returns(123);

        var sut = new OpenVpnBackgroundService(
            new Mock<ILogger<OpenVpnBackgroundService>>().Object,
            serviceProvider,
            factory,
            new VpnServerStatusManager(),
            cacheGeneration.Object,
            logStore.Object,
            CreateConfiguration());

        await InvokeRunOpenVpnTaskAsync(sut, 60, CancellationToken.None);

        presence.Verify(
            p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var eventTypes = entries
            .Select(GetEventType)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        Assert.Contains("cycle-start", eventTypes);
        Assert.Contains("cycle-plan", eventTypes);
        Assert.Contains("server-start", eventTypes);
        Assert.Contains("server-success", eventTypes);
        Assert.Contains("cycle-completed", eventTypes);
        Assert.DoesNotContain("server-error", eventTypes);
        Assert.DoesNotContain("server-timeout", eventTypes);

        var cycleCompleted = entries.First(x => GetEventType(x) == "cycle-completed");
        var root = JObject.Parse(cycleCompleted.PayloadJson);
        var metrics = root["metrics"]!;
        Assert.Equal(1, metrics["processedServers"]!.Value<int>());
        Assert.Equal(1, metrics["successServers"]!.Value<int>());
        Assert.Equal(0, metrics["timeoutServers"]!.Value<int>());
        Assert.Equal(0, metrics["failedServers"]!.Value<int>());
        Assert.Equal("service", cycleCompleted.Source);

        cacheGeneration.Verify(x => x.Bump(), Times.Once);
        ResetOpenVpnBackgroundServiceInstanceCount();
    }

    [Fact]
    public async Task RunOpenVpnTask_AppendsCycleEvents_WhenAllServersDisabled()
    {
        ResetOpenVpnBackgroundServiceInstanceCount();
        var disabledServer = new VpnServer
        {
            Id = 2,
            ServerName = "disabled-node",
            ApiUrl = "http://disabled-node",
            ServerType = VpnServerType.OpenVpn,
            IsDisable = true
        };

        var queryService = new Mock<IVpnServerQueryService>();
        queryService.Setup(x => x.GetAll(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),It.IsAny<UserVpnServerAccessOverrides?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([disabledServer]);

        var notificationService = new Mock<IServerOpenVpnNotificationService>();
        var presence = new Mock<IVpnServerClientPresenceService>();
        presence.Setup(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var serviceProvider = BuildServiceProvider(
            queryService.Object,
            notificationService.Object,
            presence.Object);

        var logStore = new Mock<IStatusStreamLogStore>();
        var entries = new List<StatusStreamLogEntry>();
        logStore.Setup(x => x.AppendAsync(It.IsAny<StatusStreamLogEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<StatusStreamLogEntry, CancellationToken>((entry, _) => entries.Add(entry));

        var sut = new OpenVpnBackgroundService(
            new Mock<ILogger<OpenVpnBackgroundService>>().Object,
            serviceProvider,
            new VpnServerProcessorFactory(serviceProvider),
            new VpnServerStatusManager(),
            new Mock<IStatusCacheGenerationService>().Object,
            logStore.Object,
            CreateConfiguration());

        await InvokeRunOpenVpnTaskAsync(sut, 30, CancellationToken.None);

        var eventTypes = entries
            .Select(GetEventType)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        Assert.Contains("cycle-start", eventTypes);
        Assert.Contains("cycle-plan", eventTypes);
        Assert.Contains("cycle-completed", eventTypes);
        Assert.DoesNotContain("server-start", eventTypes);
        Assert.DoesNotContain("server-success", eventTypes);
        presence.Verify(
            p => p.MarkAllDisconnectedAsync(disabledServer.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        ResetOpenVpnBackgroundServiceInstanceCount();
    }

    private static ServiceProvider BuildServiceProvider(
        IVpnServerQueryService vpnServerQueryService,
        IServerOpenVpnNotificationService notificationService,
        IVpnServerClientPresenceService presenceService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => vpnServerQueryService);
        services.AddScoped(_ => notificationService);
        services.AddScoped(_ => presenceService);
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenVpnPolling:MaxDegreeOfParallelism"] = "8"
            })
            .Build();

    private static async Task InvokeRunOpenVpnTaskAsync(
        OpenVpnBackgroundService sut,
        int nextRunSeconds,
        CancellationToken ct)
    {
        var method = typeof(OpenVpnBackgroundService)
            .GetMethod("RunOpenVpnTask", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        var task = method!.Invoke(sut, [nextRunSeconds, ct]) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private static string? GetEventType(StatusStreamLogEntry entry)
    {
        var root = JObject.Parse(entry.PayloadJson);
        return root["eventType"]?.Value<string>();
    }

    private static void InjectProcessor(
        VpnServerProcessorFactory factory,
        int serverId,
        VpnServerType serverType,
        IVpnServerWorkProcessor processor)
    {
        var processorsField = typeof(VpnServerProcessorFactory)
            .GetField("_processors", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(processorsField);
        var processors =
            processorsField!.GetValue(factory) as Dictionary<(int Id, VpnServerType Type), IVpnServerWorkProcessor>;
        Assert.NotNull(processors);
        processors![(serverId, serverType)] = processor;
    }

    private static void ResetOpenVpnBackgroundServiceInstanceCount()
    {
        var field = typeof(OpenVpnBackgroundService)
            .GetField("_instanceCount", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(null, 0);
    }

    private sealed class FakeProcessor : IVpnServerWorkProcessor
    {
        public Task ProcessServerAsync(VpnServer server, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
