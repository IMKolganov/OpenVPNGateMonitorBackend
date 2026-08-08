using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Helpers.Interfaces;
using Microsoft.Extensions.Logging;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.Services.Api;

internal sealed record ServerSnapshot(
    int Id,
    string ServerName,
    string ApiUrl,
    bool IsOnline,
    bool IsDefault,
    bool IsDisable,
    double? Latitude,
    double? Longitude,
    bool IsEnableWss,
    IReadOnlyList<int> QuotaPlanIds,
    IReadOnlyList<int> TagIds,
    bool HasExportConfig);

internal sealed class VpnServerLifecycleState
{
    private int _nextServerId = 1;
    private int _nextQuotaLinkId = 1;
    private int _nextTagLinkId = 1;

    public List<VpnServer> Servers { get; } = [];
    public List<QuotaPlanAllowedServer> QuotaLinks { get; } = [];
    public List<VpnServerTag> TagLinks { get; } = [];
    public List<VpnServerOvpnFileConfig> Configs { get; } = [];

    public Dictionary<int, ServerSnapshot> CaptureAllSnapshots() =>
        Servers.ToDictionary(s => s.Id, s => Snapshot(s.Id));

    public ServerSnapshot Snapshot(int serverId)
    {
        var server = Servers.Single(s => s.Id == serverId);
        return new ServerSnapshot(
            server.Id,
            server.ServerName,
            server.ApiUrl,
            server.IsOnline,
            server.IsDefault,
            server.IsDisable,
            server.Latitude,
            server.Longitude,
            server.IsEnableWss,
            QuotaLinks.Where(l => l.VpnServerId == serverId).Select(l => l.QuotaPlanId).OrderBy(x => x).ToList(),
            TagLinks.Where(l => l.VpnServerId == serverId).Select(l => l.TagId).OrderBy(x => x).ToList(),
            Configs.Any(c => c.VpnServerId == serverId));
    }

    public VpnServer GetServer(int id) => Servers.Single(s => s.Id == id);

    public void ApplyUpdateWhere(
        Expression<Func<VpnServer, bool>> predicate,
        Action<UpdateSettersBuilder<VpnServer>> setters,
        DateTimeOffset now)
    {
        var compiled = predicate.Compile();
        foreach (var server in Servers.Where(compiled))
        {
            server.IsDefault = false;
            server.LastUpdate = now;
        }
    }

    public void RemoveWhere<T>(List<T> list, Expression<Func<T, bool>> predicate)
    {
        var compiled = predicate.Compile();
        list.RemoveAll(x => compiled(x));
    }

    public int AddServer(VpnServer server)
    {
        server.Id = _nextServerId++;
        Servers.Add(server);
        return server.Id;
    }

    public void AddQuotaLinks(IEnumerable<QuotaPlanAllowedServer> links)
    {
        foreach (var link in links)
        {
            link.Id = _nextQuotaLinkId++;
            QuotaLinks.Add(link);
        }
    }

    public void AddTagLinks(IEnumerable<VpnServerTag> links)
    {
        foreach (var link in links)
        {
            link.Id = _nextTagLinkId++;
            TagLinks.Add(link);
        }
    }
}

internal sealed class VpnServerLifecycleEnvironment
{
    public VpnServerLifecycleState State { get; }
    public IVpnDataService Service { get; }

    private VpnServerLifecycleEnvironment(VpnServerLifecycleState state, IVpnDataService service)
    {
        State = state;
        Service = service;
    }

    public static VpnServerLifecycleEnvironment Create()
    {
        var state = new VpnServerLifecycleState();
        var service = BuildService(state);
        return new VpnServerLifecycleEnvironment(state, service);
    }

    public void AssertUnchangedExcept(int changedServerId, Dictionary<int, ServerSnapshot> before)
    {
        var after = State.CaptureAllSnapshots();
        foreach (var (id, snap) in before)
        {
            if (id == changedServerId)
                continue;
            VpnServerLifecycleEnvironment.AssertSnapshot(after[id], snap);
        }
    }

    public static void AssertSnapshot(ServerSnapshot actual, ServerSnapshot expected)
    {
        Assert.Equal(expected.ServerName, actual.ServerName);
        Assert.Equal(expected.ApiUrl, actual.ApiUrl);
        Assert.Equal(expected.IsOnline, actual.IsOnline);
        Assert.Equal(expected.IsDefault, actual.IsDefault);
        Assert.Equal(expected.IsDisable, actual.IsDisable);
        Assert.Equal(expected.Latitude, actual.Latitude);
        Assert.Equal(expected.Longitude, actual.Longitude);
        Assert.Equal(expected.IsEnableWss, actual.IsEnableWss);
        Assert.Equal(expected.QuotaPlanIds, actual.QuotaPlanIds);
        Assert.Equal(expected.TagIds, actual.TagIds);
        Assert.Equal(expected.HasExportConfig, actual.HasExportConfig);
    }

    private static IVpnDataService BuildService(VpnServerLifecycleState state)
    {
        var serverQ = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        var cfgQ = new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Strict);
        var quotaPlanQ = new Mock<IQuotaPlanQueryService>(MockBehavior.Strict);
        var serverCmd = new Mock<ICommandService<VpnServer, int>>(MockBehavior.Strict);
        var cfgCmd = new Mock<ICommandService<VpnServerOvpnFileConfig, int>>(MockBehavior.Strict);
        var quotaPlanCmd = new Mock<ICommandService<QuotaPlanAllowedServer, int>>(MockBehavior.Strict);
        var tagCmd = new Mock<ICommandService<VpnServerTag, int>>(MockBehavior.Strict);
        var trx = new Mock<ITransactionRunner>(MockBehavior.Strict);

        quotaPlanQ.Setup(q => q.GetDefault(It.IsAny<CancellationToken>())).ReturnsAsync((QuotaPlan?)null);

        trx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task<VpnServer>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<VpnServer>>, CancellationToken>(async (f, ct) => await f(ct));

        serverQ.Setup(q => q.AnyByServerName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) =>
                state.Servers.Any(s => s.ServerName == name && !s.IsDeleted));

        serverQ.Setup(q => q.AnyByServerNameExceptId(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, int id, CancellationToken _) =>
                state.Servers.Any(s => s.ServerName == name && s.Id != id && !s.IsDeleted));

        serverQ.Setup(q => q.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => state.Servers.FirstOrDefault(s => s.Id == id));

        serverCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) =>
            {
                state.AddServer(s);
                return s;
            });

        serverCmd.Setup(c => c.Update(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Returns((VpnServer s, bool _, CancellationToken _) =>
            {
                var idx = state.Servers.FindIndex(x => x.Id == s.Id);
                state.Servers[idx] = s;
                return Task.FromResult(1);
            });

        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<VpnServer, bool>> pred, Action<UpdateSettersBuilder<VpnServer>> _, CancellationToken _) =>
            {
                state.ApplyUpdateWhere(pred, _ => { }, DateTimeOffset.UtcNow);
                return Task.FromResult(1);
            });

        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<QuotaPlanAllowedServer, bool>> pred, CancellationToken _) =>
            {
                state.RemoveWhere(state.QuotaLinks, pred);
                return Task.FromResult(0);
            });

        quotaPlanCmd.Setup(c => c.AddRange(It.IsAny<IEnumerable<QuotaPlanAllowedServer>>(), true, It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<QuotaPlanAllowedServer> xs, bool _, CancellationToken _) =>
            {
                state.AddQuotaLinks(xs);
                return Task.FromResult(xs.Count());
            });

        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<VpnServerTag, bool>> pred, CancellationToken _) =>
            {
                state.RemoveWhere(state.TagLinks, pred);
                return Task.FromResult(0);
            });

        tagCmd.Setup(c => c.AddRange(It.IsAny<IEnumerable<VpnServerTag>>(), true, It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<VpnServerTag> xs, bool _, CancellationToken _) =>
            {
                state.AddTagLinks(xs);
                return Task.FromResult(xs.Count());
            });

        cfgQ.Setup(q => q.AnyByVpnServerId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => state.Configs.Any(c => c.VpnServerId == id));

        cfgCmd.Setup(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig cfg, bool _, CancellationToken _) =>
            {
                state.Configs.Add(cfg);
                return cfg;
            });

        var ip = new Mock<IExternalIpAddressService>();
        ip.Setup(x => x.GetRemoteIpAddress(It.IsAny<CancellationToken>())).ReturnsAsync("203.0.113.50");

        return new VpnDataService(
            Mock.Of<ILogger<IVpnDataService>>(),
            ip.Object,
            quotaPlanQ.Object,
            serverQ.Object,
            cfgQ.Object,
            trx.Object,
            serverCmd.Object,
            cfgCmd.Object,
            quotaPlanCmd.Object,
            tagCmd.Object,
            Mock.Of<IServerOpenVpnNotificationService>(),
            Mock.Of<IStatusCacheGenerationService>(),
            Mock.Of<IMicroserviceInfoService>(),
            Mock.Of<IOpenVpnMicroserviceClientFactory>(),
            Mock.Of<IOpenVpnEventClientFactory>(),
            Mock.Of<IVpnNodePublicIpLookup>(),
            Mock.Of<IVpnServerClientPresenceService>());
    }
}
