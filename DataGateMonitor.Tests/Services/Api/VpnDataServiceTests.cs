using System.Linq;
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
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;
using DataGateMonitor.SharedModels.Enums;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnDataServiceTests
{
    private static (VpnDataService svc,
        Mock<ILogger<IVpnDataService>> log,
        Mock<IQuotaPlanQueryService> quotaPlanQ,
        Mock<IVpnServerQueryService> serverQ,
        Mock<IVpnServerOvpnFileConfigQueryService> cfgQ,
        Mock<ITransactionRunner> trx,
        Mock<ICommandService<VpnServer, int>> serverCmd,
        Mock<ICommandService<VpnServerOvpnFileConfig, int>> cfgCmd,
        Mock<ICommandService<QuotaPlanAllowedServer, int>> quotaPlanCmd,
        Mock<ICommandService<VpnServerTag, int>> tagCmd,
        Mock<IServerOpenVpnNotificationService> notification,
        Mock<IStatusCacheGenerationService> statusCacheGeneration,
        Mock<IMicroserviceInfoService> microserviceInfo,
        Mock<IOpenVpnMicroserviceClientFactory> microserviceFactory,
        Mock<IOpenVpnEventClientFactory> eventFactory)
        CreateService()
    {
        var log = new Mock<ILogger<IVpnDataService>>();
        var serverQ = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        var cfgQ = new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Strict);
        var trx = new Mock<ITransactionRunner>(MockBehavior.Strict);
        var serverCmd = new Mock<ICommandService<VpnServer, int>>(MockBehavior.Strict);
        var cfgCmd = new Mock<ICommandService<VpnServerOvpnFileConfig, int>>(MockBehavior.Strict);
        var quotaPlanCmd = new Mock<ICommandService<QuotaPlanAllowedServer, int>>(MockBehavior.Strict);
        var tagCmd = new Mock<ICommandService<VpnServerTag, int>>(MockBehavior.Strict);
        var notification = new Mock<IServerOpenVpnNotificationService>(MockBehavior.Loose);
        var statusCacheGeneration = new Mock<IStatusCacheGenerationService>(MockBehavior.Loose);
        var microserviceInfo = new Mock<IMicroserviceInfoService>(MockBehavior.Loose);
        var microserviceFactory = new Mock<IOpenVpnMicroserviceClientFactory>(MockBehavior.Loose);
        var eventFactory = new Mock<IOpenVpnEventClientFactory>(MockBehavior.Loose);
        var quotaPlanQ = new Mock<IQuotaPlanQueryService>(MockBehavior.Strict);
        quotaPlanQ.Setup(q => q.GetDefault(It.IsAny<CancellationToken>())).ReturnsAsync((QuotaPlan?)null);

        trx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task<VpnServer>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<VpnServer>>, CancellationToken>(async (f, ct) => await f(ct));
        trx.Setup(t => t.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) => await f(ct));

        var svc = new VpnDataService(
            log.Object,
            quotaPlanQ.Object,
            serverQ.Object,
            cfgQ.Object,
            trx.Object,
            serverCmd.Object,
            cfgCmd.Object,
            quotaPlanCmd.Object,
            tagCmd.Object,
            notification.Object,
            statusCacheGeneration.Object,
            microserviceInfo.Object,
            microserviceFactory.Object,
            eventFactory.Object,
            Mock.Of<IVpnNodePublicIpLookup>(),
            Mock.Of<IVpnServerClientPresenceService>());

        return (svc, log, quotaPlanQ, serverQ, cfgQ, trx, serverCmd, cfgCmd, quotaPlanCmd, tagCmd, notification, statusCacheGeneration, microserviceInfo, microserviceFactory, eventFactory);
    }

    [Fact]
    public async Task AddVpnServer_DoesNotCreateDefaultConfig_DuringInitialInsert()
    {
        var (svc, _, _, serverQ, cfgQ, _, serverCmd, cfgCmd, quotaPlanCmd, tagCmd, _, _, _, _, _) = CreateService();
        var server = new VpnServer { Id = 0, IsDefault = false, ServerName = "Srv" };

        var before = DateTimeOffset.UtcNow;

        serverQ.Setup(q => q.AnyByServerName("Srv", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        serverCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServer, bool, CancellationToken>((s, _, _) => s.Id = 101)
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) => s);

        serverQ.Setup(q => q.GetById(101, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        var result = await svc.AddVpnServer(server, new List<int>(), new List<int>(), CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(101, result.Id);
        Assert.InRange(server.CreateDate, before, after);
        Assert.InRange(server.LastUpdate, before, after);

        cfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Never);
        serverCmd.Verify(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()), Times.Once);
        serverQ.Verify(q => q.GetById(101, It.IsAny<CancellationToken>()), Times.Once);
        cfgQ.Verify(q => q.AnyByVpnServerId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddVpnServer_Links_DefaultQuotaPlan_When_List_Empty_And_Default_Exists()
    {
        var (svc, _, quotaPlanQ, serverQ, cfgQ, _, serverCmd, cfgCmd, quotaPlanCmd, tagCmd, _, _, _, _, _) = CreateService();
        quotaPlanQ.Setup(q => q.GetDefault(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlan { Id = 42, Name = "Default" });

        var server = new VpnServer { Id = 0, IsDefault = false, ServerName = "Srv" };

        serverQ.Setup(q => q.AnyByServerName("Srv", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        serverCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServer, bool, CancellationToken>((s, _, _) => s.Id = 101)
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) => s);

        serverQ.Setup(q => q.GetById(101, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
        quotaPlanCmd.Setup(c => c.AddRange(It.IsAny<IEnumerable<QuotaPlanAllowedServer>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        await svc.AddVpnServer(server, new List<int>(), new List<int>(), CancellationToken.None);

        quotaPlanCmd.Verify(c => c.AddRange(
            It.Is<IEnumerable<QuotaPlanAllowedServer>>(xs =>
                xs.Single().QuotaPlanId == 42 && xs.Single().VpnServerId == 101),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
        cfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Never);
        cfgQ.Verify(q => q.AnyByVpnServerId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddVpnServer_Unsets_Previous_Default_When_IsDefault_True()
    {
        var (svc, _, _, serverQ, cfgQ, _, serverCmd, cfgCmd, quotaPlanCmd, tagCmd, _, _, _, _, _) = CreateService();
        var server = new VpnServer { Id = 0, IsDefault = true, ServerName = "DefaultSrv" };

        serverQ.Setup(q => q.AnyByServerName("DefaultSrv", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));

        serverCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServer, bool, CancellationToken>((s, _, _) => s.Id = 7)
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) => s);

        serverQ.Setup(q => q.GetById(7, It.IsAny<CancellationToken>())).ReturnsAsync(server);
        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        var result = await svc.AddVpnServer(server, new List<int>(), new List<int>(), CancellationToken.None);

        Assert.Equal(7, result.Id);
        serverCmd.Verify(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        cfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        cfgQ.Verify(q => q.AnyByVpnServerId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_Updates_Entity_And_Adds_Config_When_Missing()
    {
        var (svc, _, _, serverQ, cfgQ, _, serverCmd, cfgCmd, quotaPlanCmd, tagCmd, _, _, _, _, _) = CreateService();
        var server = new VpnServer { Id = 51, IsDefault = false, ServerName = "Srv" };

        cfgQ.Setup(q => q.AnyByVpnServerId(51, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        serverQ.Setup(q => q.AnyByServerNameExceptId("Srv", 51, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        cfgCmd.Setup(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig e, bool _, CancellationToken _) => e);

        serverCmd.Setup(c => c.Update(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));
        serverQ.Setup(q => q.GetById(51, It.IsAny<CancellationToken>())).ReturnsAsync(server);
        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        var before = DateTimeOffset.UtcNow;
        var result = await svc.UpdateVpnServer(server, new List<int>(), new List<int>(), CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(51, result.Id);
        Assert.InRange(server.LastUpdate, before, after);
        cfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Once);
        serverCmd.Verify(c => c.Update(server, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_Unsets_Other_Defaults_When_IsDefault_True()
    {
        var (svc, _, _, serverQ, cfgQ, _, serverCmd, _, quotaPlanCmd, tagCmd, _, _, _, _, _) = CreateService();
        var server = new VpnServer { Id = 9, IsDefault = true, ServerName = "Srv" };

        cfgQ.Setup(q => q.AnyByVpnServerId(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        serverQ.Setup(q => q.AnyByServerNameExceptId("Srv", 9, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));

        serverCmd.Setup(c => c.Update(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>())).Returns(Task.FromResult(1));
        serverQ.Setup(q => q.GetById(9, It.IsAny<CancellationToken>())).ReturnsAsync(server);
        quotaPlanCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));
        tagCmd.Setup(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(0));

        var result = await svc.UpdateVpnServer(server, new List<int>(), new List<int>(), CancellationToken.None);
        Assert.Equal(9, result.Id);

        serverCmd.Verify(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteVpnServer_Performs_SoftDelete_WithUpdateWhere()
    {
        var (svc, _, _, serverQ, _, _, serverCmd, _, _, _, _, _, _, _, _) = CreateService();
        var entity = new VpnServer { Id = 77, ServerName = "Srv" };
        serverQ.Setup(q => q.GetById(77, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        serverCmd.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(1));

        var ok = await svc.DeleteVpnServer(77, CancellationToken.None);

        Assert.True(ok);
        serverCmd.Verify(c => c.UpdateWhere(
                It.IsAny<Expression<Func<VpnServer, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteVpnServer_Throws_When_NotFound()
    {
        var (svc, _, _, serverQ, _, _, _, _, _, _, _, _, _, _, _) = CreateService();
        serverQ.Setup(q => q.GetById(555, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServer?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteVpnServer(555, CancellationToken.None));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_CreatesDefaultConfig_ForOpenVpnServer()
    {
        var (svc, _, _, serverQ, cfgQ, _, _, cfgCmd, _, _, _, _, microserviceInfo, _, _) = CreateService();
        serverQ.Setup(q => q.GetById(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 88, ServerName = "srv88", ServerType = VpnServerType.OpenVpn });
        cfgQ.Setup(q => q.AnyByVpnServerId(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        microserviceInfo.Setup(m => m.GetInfoAsync(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                ServerType = VpnServerType.OpenVpn,
                OpenVpn = new RootOpenVpnInfoResponse
                {
                    PublicIp = "203.0.113.99",
                    Config = new ConfigInfoResponse { Port = "1297", Proto = "tcp" }
                }
            });
        cfgCmd.Setup(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VpnServerOvpnFileConfig e, bool _, CancellationToken _) => e);

        var result = await svc.RunPostAddSetupAsync(88, CancellationToken.None);

        Assert.Equal(88, result.VpnServerId);
        Assert.True(result.CreatedDefaultConfig);
        cfgCmd.Verify(c => c.Add(It.Is<VpnServerOvpnFileConfig>(x =>
                x.VpnServerId == 88
                && x.VpnServerIp == "203.0.113.99"
                && x.VpnServerPort == 1297
                && x.ConfigTemplate.Contains("proto tcp")),
            true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_ReturnsFalse_WhenDefaultConfigAlreadyExists()
    {
        var (svc, _, _, serverQ, cfgQ, _, _, cfgCmd, _, _, _, _, microserviceInfo, _, _) = CreateService();
        serverQ.Setup(q => q.GetById(89, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 89, ServerName = "srv89" });
        cfgQ.Setup(q => q.AnyByVpnServerId(89, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await svc.RunPostAddSetupAsync(89, CancellationToken.None);

        Assert.Equal(89, result.VpnServerId);
        Assert.False(result.CreatedDefaultConfig);
        cfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Never);
        microserviceInfo.Verify(m => m.GetInfoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
