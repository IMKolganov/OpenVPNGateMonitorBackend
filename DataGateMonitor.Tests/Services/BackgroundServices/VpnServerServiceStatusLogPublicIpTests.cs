using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.BackgroundServices;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataGateMonitor.Tests.Services.BackgroundServices;

public class VpnServerServiceStatusLogPublicIpTests
{
    private readonly Mock<ILogger<IVpnServerService>> _logger = new();
    private readonly Mock<IOpenVpnClientService> _clients = new(MockBehavior.Loose);
    private readonly Mock<IOpenVpnSummaryStatService> _summary = new();
    private readonly Mock<IOpenVpnVersionService> _version = new();
    private readonly Mock<IOpenVpnStateService> _state = new();
    private readonly Mock<IIssuedOvpnFileQueryService> _issuedFiles = new(MockBehavior.Loose);
    private readonly Mock<IVpnServerStatusLogQueryService> _statusQuery = new();
    private readonly Mock<IVpnServerOvpnFileConfigQueryService> _ovpnConfig = new();
    private readonly Mock<IVpnNodePublicIpLookup> _publicIp = new();
    private readonly Mock<ITransactionRunner> _tx = new(MockBehavior.Loose);
    private readonly Mock<IUserQueryService> _users = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServer, int>> _serverCmd = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServerClient, int>> _clientCmd = new(MockBehavior.Loose);
    private readonly Mock<ICommandService<VpnServerStatusLog, int>> _statusCmd = new();
    private readonly Mock<ICommandService<VpnServerClientTraffic, int>> _trafficCmd = new(MockBehavior.Loose);
    private readonly Mock<IVpnServerClientUpsertService> _upsert = new(MockBehavior.Loose);
    private readonly Mock<IConnectedClientsCounterStore> _counter = new(MockBehavior.Loose);

    private VpnServerService CreateSut() => new(
        _logger.Object,
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

    private static VpnServer Server(int id = 7) => new()
    {
        Id = id,
        ServerName = "Norway",
        ApiUrl = "https://s5.datagateapp.com/",
        ServerType = VpnServerType.OpenVpn
    };

    private void SetupHappyPathState()
    {
        _state.Setup(s => s.GetStateAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnState
            {
                UpSince = DateTimeOffset.UtcNow.AddHours(-1),
                Connected = true,
                Success = true,
                ServerLocalIp = "10.51.15.1",
                ServerRemoteIp = string.Empty
            });
        _version.Setup(v => v.GetVersionAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("2.7.4");
        _summary.Setup(s => s.GetSummaryStatsAsync(It.IsAny<VpnServer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenVpnSummaryStats { BytesIn = 10, BytesOut = 20 });
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerStatusLog?)null);
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);
    }

    [Fact]
    public async Task SaveVpnServerStatusLogAsync_UsesNodePublicIp_OverConfigAndApiUrl()
    {
        SetupHappyPathState();
        _ovpnConfig.Setup(q => q.GetByVpnServerIdId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerOvpnFileConfig { VpnServerId = 7, VpnServerIp = "212.147.232.154" });
        _publicIp.Setup(p => p.GetAsync(7, VpnServerType.OpenVpn, It.IsAny<CancellationToken>()))
            .ReturnsAsync("198.51.100.50");

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().SaveVpnServerStatusLogAsync(Server(), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal("198.51.100.50", saved!.ServerRemoteIp);
        Assert.Equal("10.51.15.1", saved.ServerLocalIp);
    }

    [Fact]
    public async Task SaveVpnServerStatusLogAsync_WhenLookupReturnsNull_FallsBackToConfigIp()
    {
        SetupHappyPathState();
        _ovpnConfig.Setup(q => q.GetByVpnServerIdId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerOvpnFileConfig { VpnServerId = 7, VpnServerIp = "212.147.232.154" });
        _publicIp.Setup(p => p.GetAsync(7, VpnServerType.OpenVpn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().SaveVpnServerStatusLogAsync(Server(), CancellationToken.None);

        Assert.Equal("212.147.232.154", saved!.ServerRemoteIp);
    }

    [Fact]
    public async Task SaveVpnServerStatusLogAsync_WhenNoPublicIpOrConfig_FallsBackToApiUrlHost()
    {
        SetupHappyPathState();
        _ovpnConfig.Setup(q => q.GetByVpnServerIdId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig?)null);
        _publicIp.Setup(p => p.GetAsync(7, VpnServerType.OpenVpn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        VpnServerStatusLog? saved = null;
        _statusCmd
            .Setup(c => c.Add(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerStatusLog, bool, CancellationToken>((e, _, _) => saved = e)
            .ReturnsAsync((VpnServerStatusLog e, bool _, CancellationToken _) => e);

        await CreateSut().SaveVpnServerStatusLogAsync(Server(), CancellationToken.None);

        Assert.Equal("s5.datagateapp.com", saved!.ServerRemoteIp);
    }

    [Fact]
    public async Task SaveVpnServerStatusLogAsync_UpdatesExistingLog_RemoteIp()
    {
        SetupHappyPathState();
        var existing = new VpnServerStatusLog
        {
            Id = 1,
            VpnServerId = 7,
            SessionId = Guid.NewGuid(),
            ServerRemoteIp = "5.22.212.200",
            ServerLocalIp = "10.51.15.1"
        };
        _statusQuery
            .Setup(q => q.GetBySessionIdAndVpnServerId(It.IsAny<Guid>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _ovpnConfig.Setup(q => q.GetByVpnServerIdId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServerOvpnFileConfig { VpnServerId = 7, VpnServerIp = "212.147.232.154" });
        _publicIp.Setup(p => p.GetAsync(7, VpnServerType.OpenVpn, It.IsAny<CancellationToken>()))
            .ReturnsAsync("203.0.113.9");
        _statusCmd
            .Setup(c => c.Update(It.IsAny<VpnServerStatusLog>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await CreateSut().SaveVpnServerStatusLogAsync(Server(), CancellationToken.None);

        Assert.Equal("203.0.113.9", existing.ServerRemoteIp);
        _statusCmd.Verify(c => c.Update(existing, true, It.IsAny<CancellationToken>()), Times.Once);
        _statusCmd.Verify(c => c.Add(It.IsAny<VpnServerStatusLog>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
