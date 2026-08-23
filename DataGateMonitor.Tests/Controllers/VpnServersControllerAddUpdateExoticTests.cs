using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using PostSetupState = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupState;
using PostSetupStatus = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupStatus;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.Tests.Services.VpnManagerReleases;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

public class VpnServersControllerAddUpdateExoticTests
{
    private readonly Mock<IVpnDataService> _vpnDataService = new();
    private readonly Mock<IVpnServerOverviewQuery> _overviewQuery = new();
    private readonly Mock<IVpnServerQueryService> _serverQuery = new();
    private readonly Mock<IVpnServerTagQueryService> _tagQuery = new();
    private readonly Mock<IOpenVpnBackgroundService> _backgroundService = new();
    private readonly Mock<IMicroserviceInfoService> _microserviceInfo = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IUserVpnServerAccessRuleQueryService> _accessRules = new();
    private readonly Mock<IVpnServerAccessQueryService> _vpnAccess = new();
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGeneration = new();
    private readonly Mock<IStatusStreamLogStore> _statusStreamLogStore = new();
    private readonly Mock<IVpnServerPostSetupService> _postSetup = new();
    private readonly VpnServersController _controller;

    public VpnServersControllerAddUpdateExoticTests()
    {
        _accessRules.Setup(r => r.GetOverridesByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserVpnServerAccessOverrides.None);
        _controller = new VpnServersController(
            _vpnDataService.Object,
            Mock.Of<IVpnServerDiscoveryService>(),
            _overviewQuery.Object,
            _serverQuery.Object,
            _tagQuery.Object,
            _backgroundService.Object,
            _microserviceInfo.Object,
            _userQuotaPlan.Object,
            _accessRules.Object,
            _vpnAccess.Object,
            new ApiMemoryCacheService(new MemoryCache(new MemoryCacheOptions())),
            _statusCacheGeneration.Object,
            _statusStreamLogStore.Object,
            _postSetup.Object,
            Mock.Of<IConnectedClientsCounterStore>(),
            Mock.Of<IVpnServerOvpnFileConfigQueryService>(),
            new NoOpVpnManagerUpdateStatusEnricher());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "mock"))
            }
        };
    }

    [Fact]
    public async Task AddServer_PassesQuotaPlanIdsAndTagIds_ToService()
    {
        List<int>? capturedPlans = null;
        List<int>? capturedTags = null;
        _vpnDataService
            .Setup(s => s.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServer, List<int>, List<int>, CancellationToken>((_, plans, tags, _) =>
            {
                capturedPlans = plans;
                capturedTags = tags;
            })
            .ReturnsAsync(new VpnServer { Id = 100, ServerName = "new" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(100, It.IsAny<CancellationToken>())).ReturnsAsync(["edge"]);

        var req = new AddServerRequest
        {
            ServerName = "new",
            ApiUrl = "https://api.example",
            IsOnline = true,
            QuotaPlanIds = [1, 2, 2],
            TagIds = [9]
        };

        var result = await _controller.AddServer(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<VpnServerResponse>>(ok.Value);
        Assert.True(body.Success);
        Assert.Equal([1, 2, 2], capturedPlans);
        Assert.Equal([9], capturedTags);
        Assert.Equal(["edge"], body.Data!.VpnServer.Tags);
    }

    [Fact]
    public async Task UpdateServer_PassesQuotaPlanIds_ToService_PreventingSilentWipe()
    {
        List<int>? capturedPlans = null;
        _vpnDataService
            .Setup(s => s.UpdateVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServer, List<int>, List<int>, CancellationToken>((_, plans, _, _) => capturedPlans = plans)
            .ReturnsAsync(new VpnServer { Id = 5, ServerName = "patched" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(5, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var req = new UpdateServerRequest
        {
            Id = 5,
            ServerName = "patched",
            ApiUrl = "https://api.example",
            IsOnline = false,
            QuotaPlanIds = [3, 4]
        };

        await _controller.UpdateServer(req, CancellationToken.None);

        Assert.Equal([3, 4], capturedPlans);
        _vpnDataService.Verify(
            s => s.UpdateVpnServer(
                It.Is<VpnServer>(v => v.Id == 5 && v.ServerName == "patched"),
                It.IsAny<List<int>>(),
                It.IsAny<List<int>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateServer_WithEmptyQuotaPlanIds_ForwardsEmptyList()
    {
        List<int>? capturedPlans = null;
        _vpnDataService
            .Setup(s => s.UpdateVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .Callback<VpnServer, List<int>, List<int>, CancellationToken>((_, plans, _, _) => capturedPlans = plans)
            .ReturnsAsync(new VpnServer { Id = 6, ServerName = "cleared" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(6, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var req = new UpdateServerRequest
        {
            Id = 6,
            ServerName = "cleared",
            QuotaPlanIds = []
        };

        await _controller.UpdateServer(req, CancellationToken.None);

        Assert.NotNull(capturedPlans);
        Assert.Empty(capturedPlans!);
    }

    [Fact]
    public async Task AddServer_WhenServiceThrows_PropagatesException()
    {
        _vpnDataService
            .Setup(s => s.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("duplicate"));

        var req = new AddServerRequest { ServerName = "dup" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.AddServer(req, CancellationToken.None));
    }

    [Fact]
    public async Task StartPostSetup_MapsInternalStatus_ToSharedModelsResponse()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _postSetup
            .Setup(s => s.StartAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostSetupStatus
            {
                OperationId = "abc123",
                VpnServerId = 77,
                State = PostSetupState.Queued,
                Message = "Post-create setup queued.",
                CurrentStep = "queued",
                StartedAtUtc = startedAt,
                Details = { ["hint"] = "wait" }
            });

        var result = await _controller.StartPostSetup(77, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ApiResponse<VpnServerPostSetupStatusResponse>>(ok.Value);
        Assert.Equal("abc123", body.Data!.OperationId);
        Assert.Equal(VpnServerPostSetupState.Queued, body.Data.State);
        Assert.Equal("wait", body.Data.Details!["hint"]);
        Assert.Equal(startedAt, body.Data.StartedAtUtc);
    }

    [Fact]
    public async Task GetPostSetupStatus_RejectsMismatchedOperation_ForDifferentServer()
    {
        _postSetup
            .Setup(s => s.GetStatusAsync(1, "op-x", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostSetupStatus?)null);

        var result = await _controller.GetPostSetupStatus(1, "op-x", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
