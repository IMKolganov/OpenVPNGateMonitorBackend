using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.PostSetup;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;
using DataGateMonitor.SharedModels.DataGateXRayManager.Info;
using DataGateMonitor.SharedModels.Enums;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnDataServiceAddUpdateExoticTests
{
    [Fact]
    public async Task AddVpnServer_Throws_When_ServerNameAlreadyExists()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.AnyByServerName("dup", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddVpnServer(new VpnServer { ServerName = "dup" }, [], [], CancellationToken.None));

        Assert.Contains("same name", ex.Message, StringComparison.OrdinalIgnoreCase);
        h.ServerCmd.Verify(c => c.Add(It.IsAny<VpnServer>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddVpnServer_UsesExplicitQuotaPlans_AndSkipsDefaultPlanLookup()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupInsertServer("alpha", 201);
        var svc = h.Create();

        await svc.AddVpnServer(new VpnServer { ServerName = "alpha" }, [3, 7, 3], [], CancellationToken.None);

        h.QuotaPlanQ.Verify(q => q.GetDefault(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(2, h.QuotaLinksAdded.Count);
        Assert.All(h.QuotaLinksAdded, l => Assert.Equal(201, l.VpnServerId));
        Assert.Contains(h.QuotaLinksAdded, l => l.QuotaPlanId == 3);
        Assert.Contains(h.QuotaLinksAdded, l => l.QuotaPlanId == 7);
        h.Notification.Verify(n => n.NotifyAdded(201, "alpha", It.IsAny<CancellationToken>()), Times.Once);
        h.StatusCache.Verify(s => s.Bump(), Times.Once);
    }

    [Fact]
    public async Task AddVpnServer_LinksTags_AndDeduplicatesIds()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupInsertServer("tagged", 202);
        var svc = h.Create();

        await svc.AddVpnServer(new VpnServer { ServerName = "tagged" }, [], [9, 9, 11], CancellationToken.None);

        Assert.Equal(2, h.TagsAdded.Count);
        Assert.All(h.TagsAdded, t => Assert.Equal(202, t.VpnServerId));
        Assert.Contains(h.TagsAdded, t => t.TagId == 9);
        Assert.Contains(h.TagsAdded, t => t.TagId == 11);
    }

    [Fact]
    public async Task AddVpnServer_WithNoPlansAndNoDefault_CreatesNoQuotaLinks()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupInsertServer("lonely", 203);
        var svc = h.Create();

        await svc.AddVpnServer(new VpnServer { ServerName = "lonely" }, [], [], CancellationToken.None);

        h.QuotaPlanCmd.Verify(c => c.AddRange(It.IsAny<IEnumerable<QuotaPlanAllowedServer>>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddVpnServer_Throws_When_GetByIdReturnsNull_AfterInsert()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.AnyByServerName("ghost", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.ServerCmd.Setup(c => c.Add(It.IsAny<VpnServer>(), true, It.IsAny<CancellationToken>()))
            .Callback<VpnServer, bool, CancellationToken>((s, _, _) => s.Id = 204)
            .ReturnsAsync((VpnServer s, bool _, CancellationToken _) => s);
        h.ServerQ.Setup(q => q.GetById(204, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServer?)null);
        var svc = h.Create();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddVpnServer(new VpnServer { ServerName = "ghost" }, [], [], CancellationToken.None));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateVpnServer_Throws_When_NameTakenByAnotherServer()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(55, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 55, ServerName = "old", ApiUrl = "https://a/" });
        h.ServerQ.Setup(q => q.AnyByServerNameExceptId("taken", 55, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateVpnServer(new VpnServer { Id = 55, ServerName = "taken" }, [1], [], CancellationToken.None));

        Assert.Contains("same name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateVpnServer_ReplacesQuotaPlans_ForServerOnly()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(77, "srv77");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(77, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        await svc.UpdateVpnServer(new VpnServer { Id = 77, ServerName = "srv77" }, [5, 6], [], CancellationToken.None);

        h.QuotaPlanCmd.Verify(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, h.QuotaLinksAdded.Count);
        Assert.All(h.QuotaLinksAdded, l => Assert.Equal(77, l.VpnServerId));
        h.MicroserviceFactory.Verify(f => f.Invalidate(77), Times.Once);
        h.EventFactory.Verify(f => f.Remove(77), Times.Once);
        h.Notification.Verify(n => n.NotifyUpdated(77, "srv77", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateVpnServer_WithEmptyQuotaList_WipesAssignments()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(78, "srv78");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(78, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        await svc.UpdateVpnServer(new VpnServer { Id = 78, ServerName = "srv78" }, [], [], CancellationToken.None);

        h.QuotaPlanCmd.Verify(c => c.DeleteWhere(It.IsAny<Expression<Func<QuotaPlanAllowedServer, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        h.QuotaPlanCmd.Verify(c => c.AddRange(It.IsAny<IEnumerable<QuotaPlanAllowedServer>>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_ReplacesTags_OnEachSave()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(79, "srv79");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(79, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        await svc.UpdateVpnServer(new VpnServer { Id = 79, ServerName = "srv79" }, [], [2, 4], CancellationToken.None);

        h.TagCmd.Verify(c => c.DeleteWhere(It.IsAny<Expression<Func<VpnServerTag, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, h.TagsAdded.Count);
    }

    [Fact]
    public async Task UpdateVpnServer_CreatesOpenVpnConfig_WhenMissing_AndIpServiceFails()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(80, "srv80");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(80, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(80, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));
        var svc = h.Create();

        await svc.UpdateVpnServer(
            new VpnServer { Id = 80, ServerName = "srv80", ServerType = VpnServerType.OpenVpn },
            [],
            [],
            CancellationToken.None);

        Assert.Single(h.ConfigsAdded);
        Assert.Equal(string.Empty, h.ConfigsAdded[0].VpnServerIp);
        Assert.Equal(80, h.ConfigsAdded[0].VpnServerId);
        Assert.Contains("proto tcp", h.ConfigsAdded[0].ConfigTemplate);
    }

    [Fact]
    public async Task UpdateVpnServer_CreatesXrayDefaultConfig_WithPort443AndTemplate()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(81, "xray81");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(81, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(81, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                ServerType = VpnServerType.Xray,
                Xray = new RootXrayInfoResponse { PublicIp = "  " }
            });
        var svc = h.Create();

        await svc.UpdateVpnServer(
            new VpnServer { Id = 81, ServerName = "xray81", ServerType = VpnServerType.Xray },
            [],
            [],
            CancellationToken.None);

        var cfg = Assert.Single(h.ConfigsAdded);
        Assert.Equal(string.Empty, cfg.VpnServerIp);
        Assert.Equal(443, cfg.VpnServerPort);
        Assert.Contains("{{vless_uri}}", cfg.ConfigTemplate);
    }

    [Fact]
    public async Task UpdateVpnServer_UnsetsOtherDefaults_WhenMarkedDefault()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(82, "def82");
        h.SetupDefaultUnsetOnUpdate(82);
        h.CfgQ.Setup(q => q.AnyByVpnServerId(82, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        await svc.UpdateVpnServer(new VpnServer { Id = 82, ServerName = "def82", IsDefault = true }, [], [], CancellationToken.None);

        h.ServerCmd.Verify(c => c.UpdateWhere(
            It.IsAny<Expression<Func<VpnServer, bool>>>(),
            It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_Throws_WhenServerNotFound()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(999, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServer?)null);
        var svc = h.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RunPostAddSetupAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task RunPostAddSetupAsync_OpenVpn_InvokesMicroserviceAutoDetect_WhenCreatingConfig()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(90, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 90, ServerName = "ovpn90", ServerType = VpnServerType.OpenVpn });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(90, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(90, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                ServerType = VpnServerType.OpenVpn,
                OpenVpn = new RootOpenVpnInfoResponse
                {
                    PublicIp = "198.51.100.10",
                    Config = new DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info.ConfigInfoResponse { Port = "1390", Proto = "tcp" }
                }
            });
        var svc = h.Create();

        var result = await svc.RunPostAddSetupAsync(90, CancellationToken.None);

        Assert.True(result.CreatedDefaultConfig);
        var config = Assert.Single(h.ConfigsAdded);
        Assert.Equal("198.51.100.10", config.VpnServerIp);
        Assert.Equal(1390, config.VpnServerPort);
        Assert.Contains("proto tcp", config.ConfigTemplate);
        h.MicroserviceInfo.Verify(m => m.GetInfoAsync(90, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_OpenVpn_SkipsAutoDetect_WhenDiagnosticsNotOpenVpn()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(94, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 94, ServerName = "ovpn94", ServerType = VpnServerType.OpenVpn });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(94, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(94, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                ServerType = VpnServerType.Xray,
                OpenVpn = new RootOpenVpnInfoResponse { Config = new DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info.ConfigInfoResponse { Port = "1390", Proto = "tcp" } }
            });
        var svc = h.Create();

        await svc.RunPostAddSetupAsync(94, CancellationToken.None);

        Assert.Equal(1194, Assert.Single(h.ConfigsAdded).VpnServerPort);
        Assert.Equal(string.Empty, Assert.Single(h.ConfigsAdded).VpnServerIp);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_OpenVpn_Continues_WhenMicroserviceInfoThrows()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(91, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 91, ServerName = "ovpn91", ServerType = VpnServerType.OpenVpn });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(91, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(91, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("401 Unauthorized"));
        var svc = h.Create();

        var result = await svc.RunPostAddSetupAsync(91, CancellationToken.None);

        Assert.True(result.CreatedDefaultConfig);
        Assert.Equal(string.Empty, Assert.Single(h.ConfigsAdded).VpnServerIp);
        Assert.Contains("proto tcp", Assert.Single(h.ConfigsAdded).ConfigTemplate);
    }

    [Fact]
    public async Task AddVpnServer_DoesNotUnsetDefaults_When_IsDefaultFalse()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupInsertServer("regular", 205);
        var svc = h.Create();

        await svc.AddVpnServer(new VpnServer { ServerName = "regular", IsDefault = false }, [], [], CancellationToken.None);

        h.ServerCmd.Verify(c => c.UpdateWhere(
            It.IsAny<Expression<Func<VpnServer, bool>>>(),
            It.IsAny<Action<UpdateSettersBuilder<VpnServer>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateVpnServer_SkipsDefaultConfigCreation_WhenConfigAlreadyExists()
    {
        var h = new VpnDataServiceTestHarness();
        h.SetupUpdateServer(83, "srv83");
        h.CfgQ.Setup(q => q.AnyByVpnServerId(83, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = h.Create();

        await svc.UpdateVpnServer(new VpnServer { Id = 83, ServerName = "srv83" }, [1], [], CancellationToken.None);

        h.CfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Never);
        h.MicroserviceInfo.Verify(m => m.GetInfoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_Xray_CreatesDefaultExportConfig()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(95, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 95, ServerName = "xray95", ServerType = VpnServerType.Xray });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(95, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        h.MicroserviceInfo.Setup(m => m.GetInfoAsync(95, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnMicroserviceDiagnosticsDto
            {
                ServerType = VpnServerType.Xray,
                Xray = new RootXrayInfoResponse { PublicIp = "203.0.113.5" }
            });
        var svc = h.Create();

        var result = await svc.RunPostAddSetupAsync(95, CancellationToken.None);

        Assert.True(result.CreatedDefaultConfig);
        Assert.Equal(VpnServerType.Xray, result.ServerType);
        var cfg = Assert.Single(h.ConfigsAdded);
        Assert.Equal("203.0.113.5", cfg.VpnServerIp);
        Assert.Equal(443, cfg.VpnServerPort);
        Assert.Contains("{{vless_uri}}", cfg.ConfigTemplate);
    }

    [Fact]
    public async Task RunPostAddSetupAsync_ReturnsFalse_ForUnsupportedServerType()
    {
        var h = new VpnDataServiceTestHarness();
        h.ServerQ.Setup(q => q.GetById(93, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 93, ServerName = "unknown", ServerType = (VpnServerType)999 });
        h.CfgQ.Setup(q => q.AnyByVpnServerId(93, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var svc = h.Create();

        var result = await svc.RunPostAddSetupAsync(93, CancellationToken.None);

        Assert.False(result.CreatedDefaultConfig);
        h.CfgCmd.Verify(c => c.Add(It.IsAny<VpnServerOvpnFileConfig>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }
}
