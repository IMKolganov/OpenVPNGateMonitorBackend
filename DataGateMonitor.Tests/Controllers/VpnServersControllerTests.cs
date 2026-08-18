using System;
using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Newtonsoft.Json.Linq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using PostSetupStatus = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupStatus;
using PostSetupState = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupState;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

/// <summary>
/// Legacy Android compatibility regression tests.
/// These checks protect behavior required by already-installed old Android app versions.
/// </summary>
public class VpnServersControllerTests
{
    private readonly Mock<IVpnDataService> _vpnDataService = new();
    private readonly Mock<IVpnServerOverviewQuery> _overviewQuery = new();
    private readonly Mock<IVpnServerQueryService> _serverQuery = new();
    private readonly Mock<IVpnServerTagQueryService> _tagQuery = new();
    private readonly Mock<IVpnServerOvpnFileConfigQueryService> _ovpnConfigQuery = new();
    private readonly Mock<IOpenVpnBackgroundService> _backgroundService = new();
    private readonly Mock<IMicroserviceInfoService> _microserviceInfo = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IVpnServerAccessQueryService> _vpnAccess = new();
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGeneration = new();
    private readonly Mock<IStatusStreamLogStore> _statusStreamLogStore = new();
    private readonly Mock<IVpnServerPostSetupService> _vpnServerPostSetupService = new();
    private readonly IApiMemoryCacheService _cache = new ApiMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    private readonly VpnServersController _controller;

    public VpnServersControllerTests()
    {
        _controller = new VpnServersController(
            _vpnDataService.Object,
            _overviewQuery.Object,
            _serverQuery.Object,
            _tagQuery.Object,
            _backgroundService.Object,
            _microserviceInfo.Object,
            _userQuotaPlan.Object,
            _vpnAccess.Object,
            _cache,
            _statusCacheGeneration.Object,
            _statusStreamLogStore.Object,
            _vpnServerPostSetupService.Object,
            Mock.Of<IConnectedClientsCounterStore>(),
            _ovpnConfigQuery.Object);
        _ovpnConfigQuery
            .Setup(q => q.GetConfigTemplatesByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>());
        SetUserAsAdmin(_controller);
    }

    private static void SetUserAsAdmin(VpnServersController controller) =>
        SetUser(controller, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")],
            "mock")));

    private static void SetUser(VpnServersController controller, ClaimsPrincipal user)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private static JObject ParseLegacyJsonResponse(ActionResult actionResult)
    {
        var content = Assert.IsType<ContentResult>(actionResult);
        Assert.Equal("application/json", content.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(content.Content));
        return JObject.Parse(content.Content!);
    }

    [Fact]
    public async Task GetAllServersWithStatus_Returns_Ok()
    {
        _overviewQuery
            .Setup(q => q.GetAllVpnServersWithStatusAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VpnServerWithStatusDto>());
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await _controller.GetAllServersWithStatus(includeDeleted: false, CancellationToken.None);

        var json = ParseLegacyJsonResponse(result);
        Assert.True(json.Value<bool>("success"));
        Assert.NotNull(json["data"]?["openVpnServerWithStatuses"]);
        _overviewQuery.Verify(q => q.GetAllVpnServersWithStatusAsync(false, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServersWithStatus_WhenIncludeDeletedTrue_PassesTrue()
    {
        _overviewQuery
            .Setup(q => q.GetAllVpnServersWithStatusAsync(true, It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VpnServerWithStatusDto>());
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        await _controller.GetAllServersWithStatus(includeDeleted: true, CancellationToken.None);

        _overviewQuery.Verify(q => q.GetAllVpnServersWithStatusAsync(true, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public void GetAllServersWithStatus_UsesLegacyAndroidRequestRouteTemplate()
    {
        var method = typeof(VpnServersController).GetMethod(nameof(VpnServersController.GetAllServersWithStatus));
        Assert.NotNull(method);
        var route = method!
            .GetCustomAttributes<HttpGetAttribute>(inherit: true)
            .Single();

        Assert.Equal("get-all-with-status", route.Template);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task GetAllServersWithStatus_ReturnsLegacyAndroidResponseShape()
    {
        var withStatus = new VpnServerWithStatusDto
        {
            VpnServerResponses = new VpnServerResponse
            {
                VpnServer = new VpnServerDto
                {
                    Id = 7,
                    ServerName = "legacy-ru-1",
                    IsOnline = true,
                    IsDefault = false,
                    ApiUrl = "https://legacy.example"
                }
            },
            VpnServerStatusLogResponse = new VpnServerStatusLogResponse
            {
                VpnServerId = 7,
                SessionId = Guid.NewGuid(),
                BytesIn = 12,
                BytesOut = 34
            },
            CountConnectedClients = 1,
            CountSessions = 2,
            TotalBytesIn = 100,
            TotalBytesOut = 200
        };

        _overviewQuery.Setup(q => q.GetAllVpnServersWithStatusAsync(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([withStatus]);
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>> { [7] = ["legacy"] });

        var result = await _controller.GetAllServersWithStatus(false, CancellationToken.None);

        var json = ParseLegacyJsonResponse(result);
        Assert.True(json.Value<bool>("success"));
        Assert.NotNull(json["message"]);

        var item = json["data"]?["openVpnServerWithStatuses"]?.First;
        Assert.True(item is not null, $"Unexpected legacy payload: {json}");
        Assert.Equal("legacy-ru-1", item!["openVpnServerResponses"]?["openVpnServer"]?["serverName"]?.Value<string>());
        Assert.Equal(7, item["openVpnServerResponses"]?["openVpnServer"]?["id"]?.Value<int>());
        Assert.Equal(7, item["openVpnServerStatusLogResponse"]?["vpnServerId"]?.Value<int>());
        _overviewQuery.Verify(q => q.GetAllVpnServersWithStatusAsync(false, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task GetAllServersWithStatus_HidesXrayAndUdpOpenVpnFromOvpnProtoNotTags()
    {
        VpnServerWithStatusDto Item(int id, string name, VpnServerType type) => new()
        {
            VpnServerResponses = new VpnServerResponse
            {
                VpnServer = new VpnServerDto
                {
                    Id = id,
                    ServerName = name,
                    ServerType = type,
                    IsOnline = true,
                    ApiUrl = $"https://{id}.example"
                }
            }
        };

        _overviewQuery.Setup(q => q.GetAllVpnServersWithStatusAsync(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Item(1, "Helsinki 1", VpnServerType.OpenVpn),
                Item(2, "Helsinki 2", VpnServerType.OpenVpn),
                Item(3, "Helsinki Xray", VpnServerType.Xray)
            ]);
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>
            {
                [1] = ["udp"],
                [2] = ["tcp"],
                [3] = ["xray"]
            });
        _ovpnConfigQuery.Setup(q => q.GetConfigTemplatesByVpnServerIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>
            {
                [1] = "client\nproto tcp\nremote {{server_ip}} {{server_port}}",
                [2] = "client\nproto udp\nremote {{server_ip}} {{server_port}}",
                [3] = "{{vless_uri}}"
            });

        var result = await _controller.GetAllServersWithStatus(false, CancellationToken.None);

        var json = ParseLegacyJsonResponse(result);
        var items = json["data"]?["openVpnServerWithStatuses"] as JArray;
        Assert.NotNull(items);
        Assert.Single(items!);
        Assert.Equal("Helsinki 1", items![0]?["openVpnServerResponses"]?["openVpnServer"]?["serverName"]?.Value<string>());
    }

    [Fact]
    public async Task GetServerWithStatus_Returns_Ok()
    {
        var dto = new VpnServerWithStatusDto
        {
            VpnServerResponses = new VpnServerResponse
            {
                VpnServer = new VpnServerDto
                {
                    Id = 5,
                    ServerName = "srv5",
                    IsOnline = true,
                    IsDefault = false,
                    ApiUrl = "https://example.com"
                }
            },
            CountConnectedClients = 10,
            CountSessions = 20,
            TotalBytesIn = 1000,
            TotalBytesOut = 2000
        };

        _overviewQuery
            .Setup(q => q.GetVpnServerWithStatusAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(5, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());

        var req = new GetServerWithStatsRequest { VpnServerId = 5 };
        var result = await _controller.GetServerWithStatus(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerWithStatusResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Data.VpnServerWithStatus?.VpnServerResponses?.VpnServer);
        _overviewQuery.Verify(q => q.GetVpnServerWithStatusAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServers_Returns_Ok()
    {
        _serverQuery.Setup(s => s.GetAll(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<VpnServer>());
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await _controller.GetAllServers(includeDeleted: false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServersResponse>>(ok.Value);
        Assert.True(response.Success);
        _serverQuery.Verify(s => s.GetAll(false, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetServer_Returns_Ok()
    {
        _serverQuery.Setup(s => s.GetById(10, It.IsAny<CancellationToken>())).ReturnsAsync(new VpnServer { Id = 10, ServerName = "srv" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(10, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());

        var req = new GetServerRequest { VpnServerId = 10 };
        var result = await _controller.GetServer(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerResponse>>(ok.Value);
        Assert.True(response.Success);
        _serverQuery.Verify(s => s.GetById(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetServer_WhenNotFound_Returns_NotFound()
    {
        _serverQuery.Setup(s => s.GetById(99, It.IsAny<CancellationToken>())).ReturnsAsync((VpnServer?)null);

        var req = new GetServerRequest { VpnServerId = 99 };
        var result = await _controller.GetServer(req, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerResponse>>(notFound.Value);
        Assert.False(response.Success);
        Assert.Equal("VPN server not found.", response.Message);
    }

    [Fact]
    public async Task AddServer_Returns_Ok()
    {
        _vpnDataService
            .Setup(s => s.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 1, ServerName = "added" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());

        var req = new AddServerRequest
        {
            ServerName = "added",
            ApiUrl = "https://example.com",
            IsDefault = false,
            IsOnline = true
        };

        var result = await _controller.AddServer(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerResponse>>(ok.Value);
        Assert.True(response.Success);
        _vpnDataService.Verify(
            s => s.AddVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartPostSetup_Returns_Ok_WithStatus()
    {
        _vpnServerPostSetupService
            .Setup(s => s.StartAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostSetupStatus
            {
                OperationId = "op-1",
                VpnServerId = 42,
                State = PostSetupState.Queued,
                CurrentStep = "queued",
                Message = "queued",
                StartedAtUtc = DateTimeOffset.UtcNow
            });

        var result = await _controller.StartPostSetup(42, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerPostSetupStatusResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(42, response.Data!.VpnServerId);
        _vpnServerPostSetupService.Verify(s => s.StartAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostSetupStatus_WhenFound_Returns_Ok()
    {
        _vpnServerPostSetupService
            .Setup(s => s.GetStatusAsync(42, "op-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostSetupStatus
            {
                OperationId = "op-2",
                VpnServerId = 42,
                State = PostSetupState.Running,
                CurrentStep = "running",
                Message = "running",
                StartedAtUtc = DateTimeOffset.UtcNow
            });

        var result = await _controller.GetPostSetupStatus(42, "op-2", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerPostSetupStatusResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("op-2", response.Data!.OperationId);
    }

    [Fact]
    public async Task GetPostSetupStatus_WhenMissing_Returns_NotFound()
    {
        _vpnServerPostSetupService
            .Setup(s => s.GetStatusAsync(42, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostSetupStatus?)null);

        var result = await _controller.GetPostSetupStatus(42, null, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerPostSetupStatusResponse>>(notFound.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task UpdateServer_Returns_Ok()
    {
        _vpnDataService
            .Setup(s => s.UpdateVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 2, ServerName = "updated" });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(2, It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());

        var req = new UpdateServerRequest
        {
            Id = 2,
            ServerName = "updated",
            ApiUrl = "https://example.com",
            IsDefault = true,
            IsOnline = true
        };

        var result = await _controller.UpdateServer(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerResponse>>(ok.Value);
        Assert.True(response.Success);
        _vpnDataService.Verify(
            s => s.UpdateVpnServer(It.IsAny<VpnServer>(), It.IsAny<List<int>>(), It.IsAny<List<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteServer_Returns_Ok_WithBool()
    {
        _vpnDataService
            .Setup(s => s.DeleteVpnServer(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var req = new DeleteServerRequest { VpnServerId = 3 };
        var result = await _controller.DeleteServer(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.True(response.Success);
        Assert.True(response.Data);
        _vpnDataService.Verify(s => s.DeleteVpnServer(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStatus_Returns_Ok_WithStatuses()
    {
        var dict = new Dictionary<int, ServiceStatusDto>
        {
            [1] = new ServiceStatusDto { VpnServerId = 1, Status = ServiceStatus.Idle }
        };

        _backgroundService.Setup(b => b.GetStatus()).Returns(dict);

        var result = _controller.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ServiceStatusesResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.ServiceStatuses);
        _backgroundService.Verify(b => b.GetStatus(), Times.Once);
    }

    [Fact]
    public async Task RunNow_WhenNotRunning_Invokes_BackgroundService()
    {
        var dict = new Dictionary<int, ServiceStatusDto>
        {
            [1] = new ServiceStatusDto { VpnServerId = 1, Status = ServiceStatus.Idle },
            [2] = new ServiceStatusDto { VpnServerId = 2, Status = ServiceStatus.Idle },
        };

        _backgroundService.Setup(b => b.GetStatus()).Returns(dict);
        _backgroundService.Setup(b => b.RunNow(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.RunNow(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(ok.Value);
        Assert.True(response.Success);
        _backgroundService.Verify(b => b.RunNow(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunNow_WhenAnyRunning_DoesNotInvoke_Run()
    {
        var dict = new Dictionary<int, ServiceStatusDto>
        {
            [1] = new ServiceStatusDto { VpnServerId = 1, Status = ServiceStatus.Running },
            [2] = new ServiceStatusDto { VpnServerId = 2, Status = ServiceStatus.Idle },
        };

        _backgroundService.Setup(b => b.GetStatus()).Returns(dict);

        var result = await _controller.RunNow(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(ok.Value);
        Assert.True(response.Success);
        _backgroundService.Verify(b => b.RunNow(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStatusStreamLogs_Returns_Ok_WithEntries()
    {
        _statusStreamLogStore.Setup(s => s.GetLatestAsync(300, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StatusStreamLogEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    PayloadJson = "{\"statuses\":[]}",
                    Source = "memory"
                }
            ]);

        var result = await _controller.GetStatusStreamLogs(300, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<StatusStreamLogsResponse>>(ok.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Logs);
        _statusStreamLogStore.Verify(s => s.GetLatestAsync(300, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearStatusStreamLogs_Returns_Ok_AndClearsStore()
    {
        _statusStreamLogStore
            .Setup(s => s.ClearAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ClearStatusStreamLogs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(ok.Value);
        Assert.True(response.Success);
        _statusStreamLogStore.Verify(s => s.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServersWithStatus_WhenVpnUserAndNoUserIdClaim_Returns_Unauthorized()
    {
        SetUser(_controller, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "VpnUser")],
            "mock")));

        var result = await _controller.GetAllServersWithStatus(false, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<VpnServerWithStatusesResponse>>(unauthorized.Value);
        Assert.False(response.Success);
        _overviewQuery.Verify(
            q => q.GetAllVpnServersWithStatusAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task GetAllServers_WhenVpnUserAndNoQuotaPlan_Returns_AllServers_ForLegacyAndroidCompatibility()
    {
        SetUser(_controller, new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "50")
            ],
            "mock")));
        _userQuotaPlan.Setup(u => u.GetActiveByUserId(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserQuotaPlan?)null);
        _serverQuery.Setup(s => s.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VpnServer { Id = 1, ServerName = "legacy-visible" }]);
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await _controller.GetAllServers(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServersResponse>>(ok.Value);
        Assert.True(response.Success);
        _serverQuery.Verify(s => s.GetAll(false, false, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServers_WhenVpnUserWithQuotaPlan_PassesRestrictToQuotaPlanId()
    {
        SetUser(_controller, new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "51")
            ],
            "mock")));
        _userQuotaPlan.Setup(u => u.GetActiveByUserId(51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { Id = 1, UserId = 51, QuotaPlanId = 9 });
        _serverQuery.Setup(s => s.GetAll(false, false, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await _controller.GetAllServers(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServersResponse>>(ok.Value);
        Assert.True(response.Success);
        _serverQuery.Verify(s => s.GetAll(false, false, 9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetServer_WhenVpnUserAndServerExists_Returns_Ok()
    {
        SetUser(_controller, new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "60")
            ],
            "mock")));
        _serverQuery.Setup(s => s.GetById(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnServer { Id = 10, ServerName = "S10", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow });
        _tagQuery.Setup(q => q.GetTagNamesByVpnServerId(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var result = await _controller.GetServer(new GetServerRequest { VpnServerId = 10 }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<VpnServerResponse>>(ok.Value);
        Assert.True(response.Success);
        _vpnAccess.Verify(a => a.UserHasAccessAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}

