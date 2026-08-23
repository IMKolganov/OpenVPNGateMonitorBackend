using System.Reflection;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using Microsoft.EntityFrameworkCore.Query;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.VpnEvent.Requests;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager.Events;

public class OpenVpnEventClientConnectTests
{
    private static async Task InvokeHandleEventAsync(OpenVpnEventClient client, string eventType, VpnEventRequest data)
    {
        var mi = typeof(OpenVpnEventClient).GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)mi.Invoke(client, [eventType, data])!;
        await task.ConfigureAwait(false);
    }

    [Theory]
    [InlineData("127.0.0.1:53188", "127.0.0.1:53188")]
    [InlineData("tcp4-server:127.0.0.1:53188", "127.0.0.1:53188")]
    public async Task HandleEvent_ClientConnected_PassesHookRealAddressToProxyEnrichment(string realAddress, string expectedRemoteIp)
    {
        VpnServerClient? enrichedClient = null;
        VpnServerClientUpsertPayload? upsertPayload = null;

        var services = new ServiceCollection();
        var proxyMock = new Mock<IProxyClientLookupService>();
        proxyMock.Setup(x => x.EnrichFromManagementRealAddressAsync(It.IsAny<VpnServer>(), It.IsAny<VpnServerClient>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServer, VpnServerClient, CancellationToken>((_, c, _) => enrichedClient = c)
            .Returns(Task.CompletedTask);

        var upsertMock = new Mock<IVpnServerClientUpsertService>();
        upsertMock.Setup(x => x.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServerClientUpsertPayload, CancellationToken>((p, _) => upsertPayload = p)
            .ReturnsAsync(1);

        var commandMock = new Mock<ICommandService<VpnServerClient, int>>();
        commandMock.Setup(x => x.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var logService = new Mock<IVpnEventLogService>();
        logService.Setup(x => x.SaveEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<VpnEventRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var fileQuery = new Mock<IIssuedOvpnFileQueryService>();
        fileQuery.Setup(x => x.GetExternalIdByCommonName("adg-75-test", 75, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-1");

        services.AddSingleton(logService.Object);
        services.AddSingleton(fileQuery.Object);
        services.AddSingleton(proxyMock.Object);
        services.AddSingleton(upsertMock.Object);
        services.AddSingleton(commandMock.Object);
        var root = services.BuildServiceProvider();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(() => root.CreateScope());

        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(h => h.Group("75")).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubContext = new Mock<IHubContext<OpenVpnEventHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var client = new OpenVpnEventClient(
            new VpnServer { Id = 75, ApiUrl = "https://vpn.example.com" },
            NullLogger<OpenVpnEventClient>.Instance,
            hubContext.Object,
            new Mock<IMicroserviceTokenService>().Object,
            scopeFactory.Object);

        var connectedSince = new DateTimeOffset(2026, 6, 30, 10, 42, 50, TimeSpan.Zero);
        await InvokeHandleEventAsync(client, "ClientConnected", new VpnEventRequest
        {
            CommonName = "adg-75-test",
            RealAddress = realAddress,
            VirtualAddress = "10.51.15.8",
            ConnectedSince = connectedSince
        });

        Assert.NotNull(enrichedClient);
        Assert.Equal(expectedRemoteIp, enrichedClient!.RemoteIp);
        Assert.NotNull(upsertPayload);
        Assert.Equal(expectedRemoteIp, upsertPayload!.RemoteIp);
        Assert.Equal(
            VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince("adg-75-test", expectedRemoteIp, connectedSince),
            upsertPayload.SessionId);
        proxyMock.Verify(x => x.EnrichFromManagementRealAddressAsync(It.IsAny<VpnServer>(), It.IsAny<VpnServerClient>(), It.IsAny<CancellationToken>()), Times.Once);
        upsertMock.Verify(x => x.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEvent_ClientDisconnected_UpdatesByCommonNameRealAddressAndConnectedSince()
    {
        var commandMock = new Mock<ICommandService<VpnServerClient, int>>();
        commandMock.Setup(x => x.UpdateWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        commandMock.Setup(x => x.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var logService = new Mock<IVpnEventLogService>();
        logService.Setup(x => x.SaveEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<VpnEventRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(logService.Object);
        services.AddSingleton(new Mock<IIssuedOvpnFileQueryService>().Object);
        services.AddSingleton(new Mock<IProxyClientLookupService>().Object);
        services.AddSingleton(new Mock<IVpnServerClientUpsertService>().Object);
        services.AddSingleton(commandMock.Object);
        var root = services.BuildServiceProvider();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(() => root.CreateScope());

        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(h => h.Group("75")).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubContext = new Mock<IHubContext<OpenVpnEventHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var client = new OpenVpnEventClient(
            new VpnServer { Id = 75, ApiUrl = "https://vpn.example.com" },
            NullLogger<OpenVpnEventClient>.Instance,
            hubContext.Object,
            new Mock<IMicroserviceTokenService>().Object,
            scopeFactory.Object);

        var connectedSince = new DateTimeOffset(2026, 6, 30, 10, 42, 50, TimeSpan.Zero);
        await InvokeHandleEventAsync(client, "ClientDisconnected", new VpnEventRequest
        {
            CommonName = "adg-75-test",
            RealAddress = "127.0.0.1:53188",
            ConnectedSince = connectedSince
        });

        commandMock.Verify(x => x.UpdateWhere(
            It.IsAny<System.Linq.Expressions.Expression<Func<VpnServerClient, bool>>>(),
            It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        commandMock.Verify(x => x.SaveChanges(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEvent_ClientDisconnected_PersistsFinalBytesFromEvent()
    {
        VpnEventRequest? savedEvent = null;
        Action<UpdateSettersBuilder<VpnServerClient>>? setters = null;

        var commandMock = new Mock<ICommandService<VpnServerClient, int>>();
        commandMock.Setup(x => x.UpdateWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<System.Linq.Expressions.Expression<Func<VpnServerClient, bool>>,
                Action<UpdateSettersBuilder<VpnServerClient>>, CancellationToken>((_, set, _) => setters = set)
            .ReturnsAsync(1);
        commandMock.Setup(x => x.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var logService = new Mock<IVpnEventLogService>();
        logService.Setup(x => x.SaveEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<VpnEventRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, VpnEventRequest, CancellationToken>((_, _, req, _) => savedEvent = req)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(logService.Object);
        services.AddSingleton(new Mock<IIssuedOvpnFileQueryService>().Object);
        services.AddSingleton(new Mock<IProxyClientLookupService>().Object);
        services.AddSingleton(new Mock<IVpnServerClientUpsertService>().Object);
        services.AddSingleton(commandMock.Object);
        var root = services.BuildServiceProvider();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(() => root.CreateScope());

        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(h => h.Group("75")).Returns(clientProxy.Object);
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubContext = new Mock<IHubContext<OpenVpnEventHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var client = new OpenVpnEventClient(
            new VpnServer { Id = 75, ApiUrl = "https://vpn.example.com" },
            NullLogger<OpenVpnEventClient>.Instance,
            hubContext.Object,
            new Mock<IMicroserviceTokenService>().Object,
            scopeFactory.Object);

        var connectedSince = new DateTimeOffset(2026, 6, 30, 10, 42, 50, TimeSpan.Zero);
        var disconnectedAt = connectedSince.AddMinutes(3);
        await InvokeHandleEventAsync(client, "ClientDisconnected", new VpnEventRequest
        {
            CommonName = "adg-75-test",
            RealAddress = "127.0.0.1:53188",
            ConnectedSince = connectedSince,
            DisconnectedAt = disconnectedAt,
            BytesReceived = 777_000,
            BytesSent = 666_000
        });

        Assert.NotNull(savedEvent);
        Assert.Equal(777_000, savedEvent!.BytesReceived);
        Assert.Equal(666_000, savedEvent.BytesSent);
        Assert.NotNull(setters);

        // Same finalizer used by the handler semantics: event bytes overwrite last poll.
        var row = new VpnServerClient
        {
            IsConnected = true,
            BytesReceived = 100,
            BytesSent = 50
        };
        VpnClientDisconnectFinalizer.ApplyToClient(row, savedEvent, disconnectedAt.AddSeconds(1));
        Assert.Equal(777_000, row.BytesReceived);
        Assert.Equal(666_000, row.BytesSent);
        Assert.False(row.IsConnected);
    }
}
