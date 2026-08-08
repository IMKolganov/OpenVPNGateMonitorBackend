using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.GeoLite.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Helpers.Interfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.SharedModels.DataGateMonitor.GeoLite.Dto;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

/// <summary>
/// Behavioral coverage for per-server online counters: successful polls write N,
/// empty polls / disconnect / disable paths write 0, and servers stay isolated.
/// </summary>
public class VpnServerConnectedClientsCounterBehaviorTests
{
    private readonly Mock<ILogger<IVpnServerService>> _ovpnLogger = new();
    private readonly Mock<IOpenVpnClientService> _clients = new(MockBehavior.Strict);
    private readonly Mock<IOpenVpnSummaryStatService> _summary = new(MockBehavior.Loose);
    private readonly Mock<IOpenVpnVersionService> _version = new(MockBehavior.Loose);
    private readonly Mock<IOpenVpnStateService> _state = new(MockBehavior.Loose);
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedFiles = new(MockBehavior.Loose);
    private readonly Mock<IVpnServerStatusLogQueryService> _statusQuery = new(MockBehavior.Loose);
    private readonly Mock<IVpnServerOvpnFileConfigQueryService> _ovpnConfig = new(MockBehavior.Loose);
    private readonly Mock<IVpnNodePublicIpLookup> _publicIp = new(MockBehavior.Loose);
    private readonly Mock<ITransactionRunner> _tx = new(MockBehavior.Strict);
    private readonly Mock<IUserQueryService> _users = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServer, int>> _serverCmd = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServerClient, int>> _clientCmd = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServerStatusLog, int>> _statusCmd = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServerClientTraffic, int>> _trafficCmd = new(MockBehavior.Loose);
    private readonly Mock<IVpnServerClientUpsertService> _upsert = new(MockBehavior.Loose);
    private readonly Mock<IConnectedClientsCounterStore> _counter = new(MockBehavior.Strict);

    private readonly Dictionary<int, int> _countsByServer = new();

    public VpnServerConnectedClientsCounterBehaviorTests()
    {
        _tx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) => await f(ct));

        _counter
            .Setup(c => c.SetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, CancellationToken>((id, n, _) => _countsByServer[id] = n)
            .Returns(Task.CompletedTask);

        _clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _clientCmd.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        _trafficCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClientTraffic, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClientTraffic>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _trafficCmd
            .Setup(c => c.Add(It.IsAny<VpnServerClientTraffic>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerClientTraffic e, bool _, CancellationToken _) => e);
        _trafficCmd.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        _upsert.Setup(u => u.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _issuedFiles
            .Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _users.Setup(q => q.GetByExternalId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
    }

    private VpnServerService CreateOpenVpnSut() => new(
        _ovpnLogger.Object,
        _clients.Object,
        _summary.Object,
        _version.Object,
        _state.Object,
        _issuedFiles.Object,
        _statusQuery.Object,
        _ovpnConfig.Object,
        _publicIp.Object,
        _tx.Object,
        _users.Object,
        _serverCmd.Object,
        _clientCmd.Object,
        _statusCmd.Object,
        _trafficCmd.Object,
        _upsert.Object,
        _counter.Object);

    private VpnServerClientPresenceService CreatePresenceSut() => new(
        _clientCmd.Object,
        _counter.Object,
        Mock.Of<ILogger<VpnServerClientPresenceService>>());

    private static VpnServer OpenVpnServer(int id = 7) => new()
    {
        Id = id,
        ServerName = $"ovpn-{id}",
        ApiUrl = $"https://ovpn-{id}.example/",
        ServerType = VpnServerType.OpenVpn
    };

    private static VpnServer XrayServer(int id = 11) => new()
    {
        Id = id,
        ServerName = $"xray-{id}",
        ApiUrl = $"https://xray-{id}.example/",
        ServerType = VpnServerType.Xray
    };

    private static VpnServerClient MgmtClient(string cn, string remoteIp, DateTimeOffset since) => new()
    {
        CommonName = cn,
        RemoteIp = remoteIp,
        ConnectedSince = since,
        BytesReceived = 10,
        BytesSent = 20
    };

    private void SetupOpenVpnClients(VpnServer server, params VpnServerClient[] clients)
    {
        _clients
            .Setup(c => c.GetClientsFromManagementAsync(server, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnManagementStatusResult { Clients = clients.ToList() });
    }

    private XrayVpnClientSyncService CreateXraySyncSut()
    {
        var xrayLinks = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Loose);
        xrayLinks
            .Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var geo = new Mock<IGeoLiteQueryService>(MockBehavior.Loose);
        geo.Setup(g => g.GetGeoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenVpnGeoInfo?)null);

        return new XrayVpnClientSyncService(
            Mock.Of<ILogger<XrayVpnClientSyncService>>(),
            xrayLinks.Object,
            _issuedFiles.Object,
            _users.Object,
            geo.Object,
            _tx.Object,
            _clientCmd.Object,
            _trafficCmd.Object,
            _upsert.Object,
            _counter.Object);
    }

    [Fact]
    public async Task OpenVpn_SaveConnectedClients_SetsRedisCounterToClientCount()
    {
        var server = OpenVpnServer();
        var since = DateTimeOffset.Parse("2024-06-01T12:00:00Z");
        SetupOpenVpnClients(
            server,
            MgmtClient("a", "198.51.100.1:1194", since),
            MgmtClient("b", "198.51.100.2:1194", since),
            MgmtClient("c", "198.51.100.3:1194", since));

        await CreateOpenVpnSut().SaveConnectedClientsAsync(server, CancellationToken.None);

        Assert.Equal(3, _countsByServer[7]);
        _counter.Verify(c => c.SetAsync(7, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenVpn_SaveConnectedClients_WhenNoClients_SetsRedisCounterToZero()
    {
        var server = OpenVpnServer();
        SetupOpenVpnClients(server);

        await CreateOpenVpnSut().SaveConnectedClientsAsync(server, CancellationToken.None);

        Assert.Equal(0, _countsByServer[7]);
        _counter.Verify(c => c.SetAsync(7, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenVpn_SaveConnectedClients_WhenCountDrops_UpdatesCounterToNewCount()
    {
        var server = OpenVpnServer();
        var since = DateTimeOffset.Parse("2024-06-01T12:00:00Z");
        SetupOpenVpnClients(
            server,
            MgmtClient("a", "198.51.100.1:1194", since),
            MgmtClient("b", "198.51.100.2:1194", since),
            MgmtClient("c", "198.51.100.3:1194", since));
        await CreateOpenVpnSut().SaveConnectedClientsAsync(server, CancellationToken.None);
        Assert.Equal(3, _countsByServer[7]);

        SetupOpenVpnClients(server, MgmtClient("a", "198.51.100.1:1194", since));
        await CreateOpenVpnSut().SaveConnectedClientsAsync(server, CancellationToken.None);

        Assert.Equal(1, _countsByServer[7]);
        _counter.Verify(c => c.SetAsync(7, 3, It.IsAny<CancellationToken>()), Times.Once);
        _counter.Verify(c => c.SetAsync(7, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Xray_SyncConnectedClients_SetsRedisCounterToClientCount()
    {
        var server = XrayServer();
        var since = DateTimeOffset.Parse("2024-06-01T12:00:00Z");
        var clients = new List<XrayNodeClientDto>
        {
            new() { Email = "u1@ex", RemoteAddress = "203.0.113.1:443", ConnectedSince = since },
            new() { Email = "u2@ex", RemoteAddress = "203.0.113.2:443", ConnectedSince = since },
        };

        await CreateXraySyncSut().SyncConnectedClientsAsync(server, clients, CancellationToken.None);

        Assert.Equal(2, _countsByServer[11]);
        _counter.Verify(c => c.SetAsync(11, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnlineThenServerGoesUnreachable_MarkAllDisconnected_ZerosOnlyThatServerCounter()
    {
        var onlineServer = OpenVpnServer(7);
        var otherServer = OpenVpnServer(8);
        var since = DateTimeOffset.Parse("2024-06-01T12:00:00Z");

        SetupOpenVpnClients(
            onlineServer,
            MgmtClient("a", "198.51.100.1:1194", since),
            MgmtClient("b", "198.51.100.2:1194", since));
        await CreateOpenVpnSut().SaveConnectedClientsAsync(onlineServer, CancellationToken.None);

        SetupOpenVpnClients(otherServer, MgmtClient("x", "198.51.100.9:1194", since));
        await CreateOpenVpnSut().SaveConnectedClientsAsync(otherServer, CancellationToken.None);

        Assert.Equal(2, _countsByServer[7]);
        Assert.Equal(1, _countsByServer[8]);

        _clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await CreatePresenceSut().MarkAllDisconnectedAsync(7, CancellationToken.None);

        Assert.Equal(0, _countsByServer[7]);
        Assert.Equal(1, _countsByServer[8]);
    }

    [Fact]
    public async Task OpenVpnProcessor_Success_DoesNotClearPresence_AndSavesClients()
    {
        var vpnServerService = new Mock<IVpnServerService>(MockBehavior.Strict);
        vpnServerService.Setup(s => s.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        vpnServerService.Setup(s => s.SaveConnectedClientsAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var presence = new Mock<IVpnServerClientPresenceService>(MockBehavior.Strict);
        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddSingleton(vpnServerService.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton(Mock.Of<IVpnServerConflogService>());
        var sp = services.BuildServiceProvider();

        var sut = new OpenVpnServerProcessor(Mock.Of<ILogger<OpenVpnServerProcessor>>(), sp);
        await sut.ProcessServerAsync(OpenVpnServer(), CancellationToken.None);

        vpnServerService.Verify(s => s.SaveConnectedClientsAsync(It.Is<VpnServer>(v => v.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
        presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task XrayProcessor_Success_DoesNotClearPresence_AndSyncsClients()
    {
        var payload = new XrayNodeClientsResponse
        {
            Clients =
            [
                new XrayNodeClientDto
                {
                    Email = "u@ex",
                    RemoteAddress = "203.0.113.1:443",
                    ConnectedSince = DateTimeOffset.UtcNow
                }
            ]
        };

        var xrayApi = new Mock<IXrayNodeApiClient>(MockBehavior.Strict);
        xrayApi.Setup(a => a.GetActiveClientsAsync("https://xray-11.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var sync = new Mock<IXrayVpnClientSyncService>(MockBehavior.Strict);
        sync.Setup(s => s.SyncConnectedClientsAsync(
                It.IsAny<VpnServer>(),
                payload.Clients,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var presence = new Mock<IVpnServerClientPresenceService>(MockBehavior.Strict);
        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var statusLog = new Mock<IXrayVpnServerStatusLogService>(MockBehavior.Loose);

        var services = new ServiceCollection();
        services.AddSingleton(xrayApi.Object);
        services.AddSingleton(sync.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton(presence.Object);
        services.AddSingleton(statusLog.Object);
        var sp = services.BuildServiceProvider();

        var sut = new XrayServerProcessor(Mock.Of<ILogger<XrayServerProcessor>>(), sp);
        await sut.ProcessServerAsync(XrayServer(), CancellationToken.None);

        sync.Verify(s => s.SyncConnectedClientsAsync(
            It.Is<VpnServer>(v => v.Id == 11),
            payload.Clients,
            It.IsAny<CancellationToken>()), Times.Once);
        presence.Verify(p => p.MarkAllDisconnectedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OpenVpnProcessor_OnlineThenOffline_ZerosCounterViaPresence()
    {
        var server = OpenVpnServer(21);
        var since = DateTimeOffset.Parse("2024-06-01T12:00:00Z");
        SetupOpenVpnClients(
            server,
            MgmtClient("a", "198.51.100.1:1194", since),
            MgmtClient("b", "198.51.100.2:1194", since));
        await CreateOpenVpnSut().SaveConnectedClientsAsync(server, CancellationToken.None);
        Assert.Equal(2, _countsByServer[21]);

        var vpnServerService = new Mock<IVpnServerService>();
        vpnServerService
            .Setup(s => s.SaveVpnServerStatusLogAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("node offline"));

        var realPresence = CreatePresenceSut();
        _clientCmd
            .Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var serverCmd = new Mock<ICommandService<VpnServer, int>>();
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddSingleton(vpnServerService.Object);
        services.AddSingleton(serverCmd.Object);
        services.AddSingleton<IVpnServerClientPresenceService>(realPresence);
        services.AddSingleton(Mock.Of<IVpnServerConflogService>());
        var sp = services.BuildServiceProvider();

        var sut = new OpenVpnServerProcessor(Mock.Of<ILogger<OpenVpnServerProcessor>>(), sp);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProcessServerAsync(server, CancellationToken.None));

        Assert.Equal(0, _countsByServer[21]);
    }
}
