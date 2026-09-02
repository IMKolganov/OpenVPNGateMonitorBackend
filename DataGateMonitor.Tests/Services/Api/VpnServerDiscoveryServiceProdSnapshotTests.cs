using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.DataBase.Repositories.Interfaces;
using DataGateMonitor.DataBase.Repositories.Queries.Interfaces;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnServerConflogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Serialization;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;
using DataGateMonitor.SharedModels.DataGateXRayManager.Info;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Tests.Helpers;
using OpenVpnConfigInfoResponse = DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info.ConfigInfoResponse;
using XrayConfigInfoResponse = DataGateMonitor.SharedModels.DataGateXRayManager.Info.ConfigInfoResponse;

namespace DataGateMonitor.Tests.Services.Api;

/// <summary>
/// Discovery matching against a prod DB snapshot (datagate_db_prod, schema xgb_dashopnvpn, ~2026-08-26).
/// Active fleet: Cyprus (1), Helsinki 3 (69), Norway (75), Norway xray (76), Norway 2 (77).
/// </summary>
public class VpnServerDiscoveryServiceProdSnapshotTests
{
    private const string NorwayHostIp = "212.147.232.154";
    private const string HelsinkiHostIp = "212.147.237.141";
    private const string CyprusHostIp = "164.215.15.224";

    [Fact]
    public async Task ProdSnapshot_Cyprus_DirectApiUrlRestart_MatchesId1()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://164.215.15.224:5010/",
            PublicIp = CyprusHostIp,
            SuggestedName = "cyprus-restart"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(1, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_Cyprus_TrailingSlashNormalization_MatchesId1()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://164.215.15.224:5010",
            PublicIp = CyprusHostIp
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(1, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_Helsinki_NginxFronted_NodeIdentity_MatchesId69()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{HelsinkiHostIp}:5011/",
            PublicIp = HelsinkiHostIp
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(69, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_Helsinki_ExactNginxUrl_MatchesId69()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "https://s4.datagateapp.com/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(69, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_NorwaySharedHost_5011_MatchesId75_Not77()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5011/",
            PublicIp = NorwayHostIp
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(75, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_NorwaySharedHost_5012_MatchesId77_Not75()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5012/",
            PublicIp = NorwayHostIp
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(77, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_NorwaySharedHost_UnknownPort5010_CreatesPending()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5010/",
            PublicIp = NorwayHostIp,
            SuggestedName = "norway-new-stack"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), "norway-new-stack", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_Norway_ExactNginxUrl_MatchesId75()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "https://s5.datagateapp.com/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(75, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_Norway2_ExactNginxUrl_MatchesId77()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "https://s6.datagateapp.com/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(77, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_NorwaySharedHost_DefaultHttpsPort_DoesNotFalseMatch()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"https://{NorwayHostIp}/",
            PublicIp = NorwayHostIp,
            SuggestedName = "norway-ambiguous-443"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_NorwayXray_ExactDomainUrl_MatchesId76()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.Xray,
            ApiUrl = "https://xs2.datagateapp.com/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(76, result.ExistingVpnServerId);
    }

    [Fact]
    public async Task ProdSnapshot_NorwayXray_DirectIp9443WithoutConflog_CreatesPending()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.Xray,
            ApiUrl = $"http://{NorwayHostIp}:9443/",
            PublicIp = NorwayHostIp,
            SuggestedName = "norway-xray-restart"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), "norway-xray-restart", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_NorwayXray_OpenVpnTypeMismatch_DoesNotMatchId76()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "https://xs2.datagateapp.com/"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.Pending, result.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_ManagerVersionChange_StillMatchesExistingServer()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var result = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = "http://164.215.15.224:5010/",
            PublicIp = CyprusHostIp,
            SuggestedName = "cyprus-upgraded",
            Version = "9.9.9.999"
        }, CancellationToken.None);

        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, result.Status);
        Assert.Equal(1, result.ExistingVpnServerId);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProdSnapshot_UdpAndTcpStacksOnSameIp_DistinguishedByManagerApiPort()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        // Norway OpenVpn (id=75) listens on manager 5011; Norway 2 (id=77) on 5012.
        var tcpNorway = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5011/",
            PublicIp = NorwayHostIp,
            Version = "1.0.0.1"
        }, CancellationToken.None);
        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, tcpNorway.Status);
        Assert.Equal(75, tcpNorway.ExistingVpnServerId);

        var norway2 = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5012/",
            PublicIp = NorwayHostIp,
            Version = "2.0.0.2"
        }, CancellationToken.None);
        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, norway2.Status);
        Assert.Equal(77, norway2.ExistingVpnServerId);

        // UDP-style manager port 5010 is not registered on this host in prod snapshot.
        var udpPort = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5010/",
            PublicIp = NorwayHostIp,
            Version = "3.0.0.3"
        }, CancellationToken.None);
        Assert.Equal(AnnounceVpnServerResultStatus.Pending, udpPort.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_CyprusUdp5010_DoesNotCrossMatchDeletedTcp5011Port()
    {
        await using var ctx = await SeedProdFleetAsync();
        var sut = CreateSut(ctx, out var discoveryCmd, out var notifications, out _);
        WireCommandToContext(ctx, discoveryCmd);

        // Active Cyprus is UDP/manager 5010; deleted "Cyprus TCP" row used 5011 on same IP.
        var udp = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{CyprusHostIp}:5010/",
            PublicIp = CyprusHostIp
        }, CancellationToken.None);
        Assert.Equal(AnnounceVpnServerResultStatus.AlreadyRegistered, udp.Status);
        Assert.Equal(1, udp.ExistingVpnServerId);

        var tcpPort = await sut.AnnounceAsync(new AnnounceVpnServerRequest
        {
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{CyprusHostIp}:5011/",
            PublicIp = CyprusHostIp,
            SuggestedName = "cyprus-tcp-reborn"
        }, CancellationToken.None);
        Assert.Equal(AnnounceVpnServerResultStatus.Pending, tcpPort.Status);
        notifications.Verify(
            n => n.NotifyDiscovered(It.IsAny<int>(), "cyprus-tcp-reborn", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProdSnapshot_ListPending_AutoResolvesStaleNorway5011_ToId75()
    {
        await using var ctx = await SeedProdFleetAsync();
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerDiscoveries.Add(new VpnServerDiscovery
        {
            Id = 900,
            ServerType = VpnServerType.OpenVpn,
            ApiUrl = $"http://{NorwayHostIp}:5011/",
            PublicIp = NorwayHostIp,
            SuggestedName = "stale-norway",
            Status = VpnServerDiscoveryStatus.Pending,
            LastSeenUtc = now,
            CreateDate = now,
            LastUpdate = now
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, out var discoveryCmd, out _, out _);
        WireCommandToContext(ctx, discoveryCmd);

        var list = await sut.ListPendingAsync(CancellationToken.None);

        Assert.Empty(list.Discoveries);
        var row = await ctx.VpnServerDiscoveries.FindAsync(900);
        Assert.Equal(VpnServerDiscoveryStatus.Approved, row!.Status);
        Assert.Equal(75, row.ResolvedVpnServerId);
    }

    private static async Task<ApplicationDbContext> SeedProdFleetAsync()
    {
        var ctx = CreateContext();

        SeedServer(ctx, 1, "🇨🇾 Cyprus", "http://164.215.15.224:5010/", deleted: false);
        SeedOvpnConfig(ctx, 1, CyprusHostIp);
        SeedConflog(ctx, 1, VpnServerType.OpenVpn, 5010);

        SeedServer(ctx, 69, "🇫🇮 Helsinki 3", "https://s4.datagateapp.com/", deleted: false);
        SeedOvpnConfig(ctx, 69, HelsinkiHostIp);
        SeedConflog(ctx, 69, VpnServerType.OpenVpn, 5011);

        SeedServer(ctx, 75, "🇳🇴 Norway", "https://s5.datagateapp.com/", deleted: false);
        SeedOvpnConfig(ctx, 75, NorwayHostIp);
        SeedConflog(ctx, 75, VpnServerType.OpenVpn, 5011);

        SeedServer(ctx, 76, "🇳🇴 Norway xray", "https://xs2.datagateapp.com", deleted: false,
            serverType: VpnServerType.Xray);
        SeedOvpnConfig(ctx, 76, "xs2.datagateapp.com");

        SeedServer(ctx, 77, "🇳🇴 Norway 2", "https://s6.datagateapp.com/", deleted: false);
        SeedOvpnConfig(ctx, 77, NorwayHostIp);
        SeedConflog(ctx, 77, VpnServerType.OpenVpn, 5012);

        // Deleted rows from prod — must not participate in matching.
        SeedServer(ctx, 2, "🇨🇾 Cyprus TCP", "http://164.215.15.224:5011/", deleted: true);
        SeedServer(ctx, 78, "Cyprus main", "http://164.215.15.224:5010/", deleted: true);

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static void SeedServer(
        ApplicationDbContext ctx,
        int id,
        string name,
        string apiUrl,
        bool deleted,
        VpnServerType serverType = VpnServerType.OpenVpn)
    {
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServers.Add(new VpnServer
        {
            Id = id,
            ServerName = name,
            ApiUrl = apiUrl,
            ServerType = serverType,
            IsDeleted = deleted,
            CreateDate = now,
            LastUpdate = now
        });
    }

    private static void SeedOvpnConfig(ApplicationDbContext ctx, int vpnServerId, string vpnServerIp)
    {
        var now = DateTimeOffset.UtcNow;
        ctx.VpnServerOvpnFileConfigs.Add(new VpnServerOvpnFileConfig
        {
            VpnServerId = vpnServerId,
            VpnServerIp = vpnServerIp,
            VpnServerPort = 1194,
            ConfigTemplate = "remote {{server_ip}} {{server_port}}",
            CreateDate = now,
            LastUpdate = now
        });
    }

    private static void SeedConflog(ApplicationDbContext ctx, int vpnServerId, VpnServerType serverType, int apiPort)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = ProjectJson.Serialize(new VpnMicroserviceDiagnosticsDto
        {
            ServerType = serverType,
            OpenVpn = serverType == VpnServerType.OpenVpn
                ? new RootOpenVpnInfoResponse
                {
                    Config = new OpenVpnConfigInfoResponse { ApiPort = apiPort.ToString() }
                }
                : null,
            Xray = serverType == VpnServerType.Xray
                ? new RootXrayInfoResponse
                {
                    Config = new XrayConfigInfoResponse { ApiPort = apiPort.ToString() }
                }
                : null
        });

        ctx.VpnServerConflogs.Add(new VpnServerConflog
        {
            VpnServerId = vpnServerId,
            RequestUrl = $"https://server-{vpnServerId}.example/",
            PayloadJson = payload,
            CreateDate = now,
            LastUpdate = now
        });
    }

    private static void WireCommandToContext(
        ApplicationDbContext ctx,
        Mock<ICommandService<VpnServerDiscovery, int>> discoveryCmd)
    {
        discoveryCmd
            .Setup(c => c.Add(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerDiscovery d, bool _, CancellationToken _) =>
            {
                if (d.Id == 0)
                {
                    var next = ctx.VpnServerDiscoveries.AsEnumerable().Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
                    d.Id = next;
                }

                ctx.VpnServerDiscoveries.Add(d);
                ctx.SaveChanges();
                return d;
            });
        discoveryCmd
            .Setup(c => c.Update(It.IsAny<VpnServerDiscovery>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerDiscovery d, bool _, CancellationToken _) =>
            {
                ctx.VpnServerDiscoveries.Update(d);
                return ctx.SaveChanges();
            });
    }

    private static VpnServerDiscoveryService CreateSut(
        ApplicationDbContext ctx,
        out Mock<ICommandService<VpnServerDiscovery, int>> discoveryCmd,
        out Mock<IServerOpenVpnNotificationService> notifications,
        out Mock<IVpnDataService> vpnData)
    {
        var uow = new DbContextUnitOfWork(ctx);
        var discoveryQuery = new EfQueryService<VpnServerDiscovery, int>(uow);
        var vpnServerQuery = new EfQueryService<VpnServer, int>(uow);
        var ovpnConfigQuery = new EfQueryService<VpnServerOvpnFileConfig, int>(uow);
        var conflogQuery = new EfQueryService<VpnServerConflog, int>(uow);
        discoveryCmd = new Mock<ICommandService<VpnServerDiscovery, int>>(MockBehavior.Strict);
        notifications = new Mock<IServerOpenVpnNotificationService>(MockBehavior.Loose);
        vpnData = new Mock<IVpnDataService>(MockBehavior.Strict);
        var cache = new MemoryCache(new MemoryCacheOptions());

        return new VpnServerDiscoveryService(
            discoveryQuery,
            discoveryCmd.Object,
            vpnServerQuery,
            new VpnServerOvpnFileConfigQueryService(ovpnConfigQuery),
            new VpnServerConflogQueryService(conflogQuery),
            vpnData.Object,
            notifications.Object,
            cache,
            NullLogger<VpnServerDiscoveryService>.Instance);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBaseSettings:DefaultSchema"] = "test_schema"
            })
            .Build();
        return new ApplicationDbContext(options, configuration);
    }

    private sealed class DbContextUnitOfWork(ApplicationDbContext ctx) : IUnitOfWork
    {
        public void Dispose() { }

        public IRepository<T> GetRepository<T>() where T : class
            => throw new NotImplementedException();

        public IQuery<T> GetQuery<T>() where T : class
            => new TestQuery<T>(ctx.Set<T>());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => ctx.SaveChangesAsync(cancellationToken);
        public void SaveChanges() => ctx.SaveChanges();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public void MarkPropertyModified<T>(T entity, System.Linq.Expressions.Expression<Func<T, object>> property) where T : class
            => throw new NotImplementedException();
    }
}
