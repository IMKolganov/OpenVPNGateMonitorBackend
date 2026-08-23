using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.GeoLite.Interfaces;
using DataGateMonitor.Services.XrayNode;
using DataGateMonitor.SharedModels.DataGateMonitor.GeoLite.Dto;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Services.XrayNode;

public class XrayVpnClientSyncServiceProxyRealIpTests
{
    [Fact]
    public async Task Sync_WhenProxyRealIpPresent_PersistsProxyAndGeosOnIt()
    {
        VpnServerClientUpsertPayload? captured = null;
        string? geoHost = null;

        var upsert = new Mock<IVpnServerClientUpsertService>(MockBehavior.Strict);
        upsert.Setup(u => u.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServerClientUpsertPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);

        var geo = new Mock<IGeoLiteQueryService>(MockBehavior.Strict);
        geo.Setup(g => g.GetGeoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((h, _) => geoHost = h)
            .ReturnsAsync(new OpenVpnGeoInfo
            {
                Country = "DE",
                Region = "BE",
                City = "Berlin",
                Latitude = 1,
                Longitude = 2
            });

        var (clientCmd, trafficCmd, tx, counter) = CreateCommandMocks();

        var xrayLinks = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        xrayLinks.Setup(q => q.GetExternalIdByCommonName("cn-1", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-1");

        var users = new Mock<IUserQueryService>(MockBehavior.Strict);
        users.Setup(q => q.GetByExternalId("ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new XrayVpnClientSyncService(
            NullLogger<XrayVpnClientSyncService>.Instance,
            xrayLinks.Object,
            Mock.Of<IIssuedOvpnFileQueryService>(),
            users.Object,
            geo.Object,
            tx.Object,
            clientCmd.Object,
            trafficCmd.Object,
            upsert.Object,
            counter.Object);

        await sut.SyncConnectedClientsAsync(
            new VpnServer
            {
                Id = 7,
                ServerType = VpnServerType.Xray,
                ServerName = "x7",
                ApiUrl = "https://x/",
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow
            },
            [
                new XrayNodeClientDto
                {
                    Email = "cn-1",
                    RemoteAddress = "172.20.0.2",
                    ProxyRealIp = "203.0.113.9:443",
                    BytesReceived = 10,
                    BytesSent = 20,
                    ConnectedSince = DateTimeOffset.Parse("2024-01-01T00:00:00Z")
                }
            ],
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("172.20.0.2", captured!.RemoteIp);
        Assert.Equal("203.0.113.9:443", captured.ProxyRealIp);
        Assert.Equal("DE", captured.Country);
        Assert.Equal("203.0.113.9", geoHost);
    }

    [Fact]
    public async Task Sync_WhenPrivateRemoteWithoutProxy_DoesNotPersistProxyEnrichment()
    {
        VpnServerClientUpsertPayload? captured = null;

        var upsert = new Mock<IVpnServerClientUpsertService>(MockBehavior.Strict);
        upsert.Setup(u => u.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServerClientUpsertPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);

        var geo = new Mock<IGeoLiteQueryService>(MockBehavior.Loose);
        geo.Setup(g => g.GetGeoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenVpnGeoInfo?)null);

        var (clientCmd, trafficCmd, tx, counter) = CreateCommandMocks();
        trafficCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClientTraffic, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClientTraffic>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var xrayLinks = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        xrayLinks.Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var ovpn = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        ovpn.Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = new XrayVpnClientSyncService(
            NullLogger<XrayVpnClientSyncService>.Instance,
            xrayLinks.Object,
            ovpn.Object,
            Mock.Of<IUserQueryService>(),
            geo.Object,
            tx.Object,
            clientCmd.Object,
            trafficCmd.Object,
            upsert.Object,
            counter.Object);

        await sut.SyncConnectedClientsAsync(
            new VpnServer
            {
                Id = 1,
                ServerType = VpnServerType.Xray,
                ServerName = "x",
                ApiUrl = "https://x/",
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow
            },
            [
                new XrayNodeClientDto
                {
                    Email = "cn",
                    RemoteAddress = "172.20.0.2",
                    ProxyRealIp = null,
                    ConnectedSince = DateTimeOffset.UtcNow
                }
            ],
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured!.ProxyRealIp);
        Assert.Null(captured.Country);
        // persistProxyEnrichment=false → FromClient nulls enrichment fields so upsert COALESCE keeps prior row.
        geo.Verify(g => g.GetGeoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sync_WhenPublicRemoteWithoutProxy_GeosOnRemoteAndPersistsEnrichmentFields()
    {
        VpnServerClientUpsertPayload? captured = null;
        string? geoHost = null;

        var upsert = new Mock<IVpnServerClientUpsertService>(MockBehavior.Strict);
        upsert.Setup(u => u.UpsertAsync(It.IsAny<VpnServerClientUpsertPayload>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServerClientUpsertPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(1);

        var geo = new Mock<IGeoLiteQueryService>(MockBehavior.Strict);
        geo.Setup(g => g.GetGeoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((h, _) => geoHost = h)
            .ReturnsAsync(new OpenVpnGeoInfo { Country = "NL", Region = "NH", City = "Amsterdam" });

        var (clientCmd, trafficCmd, tx, counter) = CreateCommandMocks();
        trafficCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClientTraffic, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClientTraffic>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var xrayLinks = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        xrayLinks.Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var ovpn = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        ovpn.Setup(q => q.GetExternalIdByCommonName(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = new XrayVpnClientSyncService(
            NullLogger<XrayVpnClientSyncService>.Instance,
            xrayLinks.Object,
            ovpn.Object,
            Mock.Of<IUserQueryService>(),
            geo.Object,
            tx.Object,
            clientCmd.Object,
            trafficCmd.Object,
            upsert.Object,
            counter.Object);

        await sut.SyncConnectedClientsAsync(
            new VpnServer
            {
                Id = 2,
                ServerType = VpnServerType.Xray,
                ServerName = "x",
                ApiUrl = "https://x/",
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow
            },
            [
                new XrayNodeClientDto
                {
                    Email = "cn",
                    RemoteAddress = "203.0.113.50:443",
                    ProxyRealIp = null,
                    ConnectedSince = DateTimeOffset.UtcNow
                }
            ],
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured!.ProxyRealIp);
        Assert.Equal("NL", captured.Country);
        Assert.Equal("203.0.113.50", geoHost);
    }

    private static (
        Mock<ICommandService<VpnServerClient, int>> ClientCmd,
        Mock<ICommandService<VpnServerClientTraffic, int>> TrafficCmd,
        Mock<ITransactionRunner> Tx,
        Mock<IConnectedClientsCounterStore> Counter)
        CreateCommandMocks()
    {
        var clientCmd = new Mock<ICommandService<VpnServerClient, int>>(MockBehavior.Loose);
        clientCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClient>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        clientCmd.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var trafficCmd = new Mock<ICommandService<VpnServerClientTraffic, int>>(MockBehavior.Loose);
        trafficCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServerClientTraffic, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServerClientTraffic>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        trafficCmd.Setup(c => c.Add(It.IsAny<VpnServerClientTraffic>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerClientTraffic t, bool _, CancellationToken __) => t);
        trafficCmd.Setup(c => c.SaveChanges(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var tx = new Mock<ITransactionRunner>(MockBehavior.Strict);
        tx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) => await f(ct));

        var counter = new Mock<IConnectedClientsCounterStore>(MockBehavior.Loose);
        counter.Setup(c => c.SetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (clientCmd, trafficCmd, tx, counter);
    }
}
