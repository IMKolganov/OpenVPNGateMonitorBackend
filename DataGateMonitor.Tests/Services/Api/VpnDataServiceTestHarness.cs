using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Helpers.Interfaces;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;

namespace DataGateMonitor.Tests.Services.Api;

internal sealed class VpnDataServiceTestHarness
{
    public Mock<ILogger<IVpnDataService>> Log { get; } = new();
    public Mock<IExternalIpAddressService> Ip { get; } = new(MockBehavior.Strict);
    public Mock<IQuotaPlanQueryService> QuotaPlanQ { get; } = new(MockBehavior.Strict);
    public Mock<IVpnServerQueryService> ServerQ { get; } = new(MockBehavior.Strict);
    public Mock<IVpnServerOvpnFileConfigQueryService> CfgQ { get; } = new(MockBehavior.Strict);
    public Mock<ITransactionRunner> Trx { get; } = new(MockBehavior.Strict);
    public Mock<ICommandService<VpnServer, int>> ServerCmd { get; } = new(MockBehavior.Strict);
    public Mock<ICommandService<VpnServerOvpnFileConfig, int>> CfgCmd { get; } = new(MockBehavior.Strict);
    public Mock<ICommandService<QuotaPlanAllowedServer, int>> QuotaPlanCmd { get; } = new(MockBehavior.Strict);
    public Mock<ICommandService<VpnServerTag, int>> TagCmd { get; } = new(MockBehavior.Strict);
    public Mock<IServerOpenVpnNotificationService> Notification { get; } = new(MockBehavior.Loose);
    public Mock<IStatusCacheGenerationService> StatusCache { get; } = new(MockBehavior.Loose);
    public Mock<IMicroserviceInfoService> MicroserviceInfo { get; } = new(MockBehavior.Loose);
    public Mock<IOpenVpnMicroserviceClientFactory> MicroserviceFactory { get; } = new(MockBehavior.Loose);
    public Mock<IOpenVpnEventClientFactory> EventFactory { get; } = new(MockBehavior.Loose);
    public Mock<IVpnNodePublicIpLookup> PublicIpLookup { get; } = new(MockBehavior.Loose);
    public Mock<IVpnServerClientPresenceService> Presence { get; } = new(MockBehavior.Loose);

    public List<QuotaPlanAllowedServer> QuotaLinksAdded { get; } = [];
    public List<VpnServerTag> TagsAdded { get; } = [];
    public List<VpnServerOvpnFileConfig> ConfigsAdded { get; } = [];

    public VpnDataServiceTestHarness()
    {
        QuotaPlanQ.Setup(q => q.GetDefault(It.IsAny<CancellationToken>())).ReturnsAsync((QuotaPlan?)null);
        Trx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task<VpnServer>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<VpnServer>>, CancellationToken>(async (f, ct) => await f(ct));
        Trx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) => await f(ct));

        QuotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));

        QuotaPlanCmd.Setup(c => c.AddRange(It.IsAny<IEnumerable<QuotaPlanAllowedServer>>(), true, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<QuotaPlanAllowedServer>, bool, CancellationToken>((xs, _, _) => QuotaLinksAdded.AddRange(xs))
            .ReturnsAsync(1);

        TagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(0));
        TagCmd.Setup(c => c.AddRange(It.IsAny<IEnumerable<VpnServerTag>>(), true, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<VpnServerTag>, bool, CancellationToken>((xs, _, _) => TagsAdded.AddRange(xs))
            .ReturnsAsync(1);

        CfgCmd.Setup(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServerOvpnFileConfig, bool, CancellationToken>((cfg, _, _) => ConfigsAdded.Add(cfg))
            .ReturnsAsync((VpnServerOvpnFileConfig e, bool _, CancellationToken _) => e);
    }

    public VpnDataService Create() => new(
        Log.Object,
        Ip.Object,
        QuotaPlanQ.Object,
        ServerQ.Object,
        CfgQ.Object,
        Trx.Object,
        ServerCmd.Object,
        CfgCmd.Object,
        QuotaPlanCmd.Object,
        TagCmd.Object,
        Notification.Object,
        StatusCache.Object,
        MicroserviceInfo.Object,
        MicroserviceFactory.Object,
        EventFactory.Object,
        PublicIpLookup.Object,
        Presence.Object);

    public void SetupInsertServer(string name, int assignedId, VpnServer? returnEntity = null)
    {
        ServerQ.Setup(q => q.AnyByServerName(name, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ServerCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServer, bool, CancellationToken>((s, _, _) => s.Id = assignedId)
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) => s);
        ServerQ.Setup(q => q.GetById(assignedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnEntity ?? new VpnServer { Id = assignedId, ServerName = name });
    }

    public void SetupUpdateServer(int id, string name)
    {
        ServerQ.Setup(q => q.AnyByServerNameExceptId(name, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ServerCmd.Setup(c => c.Update(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));
        ServerQ.Setup(q => q.GetById(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = id, ServerName = name });
    }

    public void SetupDefaultUnsetOnAdd()
    {
        ServerCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));
    }

    public void SetupDefaultUnsetOnUpdate(int serverId)
    {
        ServerCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));
    }
}
