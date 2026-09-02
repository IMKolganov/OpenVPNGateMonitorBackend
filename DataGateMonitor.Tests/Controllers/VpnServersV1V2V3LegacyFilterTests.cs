using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Newtonsoft.Json.Linq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.Tests.Services.VpnManagerReleases;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers;

/// <summary>
/// Same mixed inventory (TCP OpenVPN, UDP OpenVPN via ovpn <c>proto</c>, Xray) through v1/v2/v3 lists.
/// v1 hides UDP+Xray for Android ≤1.0.4; v2/v3 must keep the full set.
/// </summary>
public class VpnServersV1V2V3LegacyFilterTests
{
    private const int TcpOpenVpnId = 1;
    private const int UdpOpenVpnId = 2;
    private const int XrayId = 3;

    private readonly Mock<IVpnServerOverviewQuery> _overviewQuery = new();
    private readonly Mock<IVpnServerQueryService> _serverQuery = new();
    private readonly Mock<IVpnServerTagQueryService> _tagQuery = new();
    private readonly Mock<IVpnServerOvpnFileConfigQueryService> _ovpnConfigQuery = new();
    private readonly Mock<IVpnServerQuotaPlanGroupsQuery> _quotaGroups = new();
    private readonly Mock<IVpnServerGroupQueryService> _groupQuery = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IQuotaPlanAllowedServerQueryService> _quotaAllowed = new();
    private readonly Mock<IUserVpnServerAccessRuleQueryService> _accessRules = new();
    private readonly Mock<IQuotaPlanQueryService> _quotaPlanQuery = new();
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGeneration = new();

    public VpnServersV1V2V3LegacyFilterTests()
    {
        _accessRules
            .Setup(r => r.GetOverridesByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserVpnServerAccessOverrides.None);

        _overviewQuery
            .Setup(q => q.GetAllVpnServersWithStatusAsync(
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),It.IsAny<UserVpnServerAccessOverrides?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory);

        _tagQuery
            .Setup(q => q.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>
            {
                [TcpOpenVpnId] = ["udp"],
                [UdpOpenVpnId] = ["tcp"],
                [XrayId] = ["xray"]
            });

        _ovpnConfigQuery
            .Setup(q => q.GetConfigTemplatesByVpnServerIds(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>
            {
                [TcpOpenVpnId] = "client\nproto tcp\nremote {{server_ip}} {{server_port}}",
                [UdpOpenVpnId] = "client\nproto udp\nremote {{server_ip}} {{server_port}}",
                [XrayId] = "{{vless_uri}}"
            });

        _quotaGroups
            .Setup(g => g.GetGroupsByVpnServerIdsAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());

        _groupQuery.Setup(g => g.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task GetAllWithStatus_V1HidesUdpAndXray_V2AndV3KeepFullInventory()
    {
        var admin = Admin();
        var v1 = CreateV1(admin);
        var v2 = CreateV2(admin);
        var v3 = CreateV3(admin);

        var v1Json = ParseLegacyJson(await v1.GetAllServersWithStatus(
            includeDeleted: false,
            ct: CancellationToken.None,
            withoutCache: true));
        var v1Names = NamesFromLegacyV1(v1Json);

        var v2Result = await v2.GetAllServersWithStatus(
            includeDeleted: false,
            ct: CancellationToken.None,
            withoutCache: true);
        var v2Api = Assert.IsType<ApiResponse<VpnServerWithStatusesV2Response>>(
            Assert.IsType<OkObjectResult>(v2Result.Result).Value);
        var v2Names = NamesFromV2(v2Api.Data!);

        var v3Result = await v3.GetAllServersWithStatus(
            includeDeleted: false,
            ct: CancellationToken.None,
            withoutCache: true);
        var v3Api = Assert.IsType<ApiResponse<VpnServerWithStatusesV3Response>>(
            Assert.IsType<OkObjectResult>(v3Result.Result).Value);
        var v3Names = NamesFromV2(v3Api.Data!.VpnServerWithStatuses);

        Assert.Equal(["Helsinki 1"], v1Names);
        Assert.Equal(["Helsinki 1", "Helsinki 2", "Helsinki Xray"], v2Names);
        Assert.Equal(["Helsinki 1", "Helsinki 2", "Helsinki Xray"], v3Names);
    }

    private VpnServersController CreateV1(ClaimsPrincipal user) =>
        new(
            Mock.Of<IVpnDataService>(),
            Mock.Of<IVpnServerDiscoveryService>(),
            _overviewQuery.Object,
            _serverQuery.Object,
            _tagQuery.Object,
            Mock.Of<IOpenVpnBackgroundService>(),
            Mock.Of<IMicroserviceInfoService>(),
            _userQuotaPlan.Object,
            _accessRules.Object,
            Mock.Of<IVpnServerAccessQueryService>(),
            NewCache(),
            _statusCacheGeneration.Object,
            Mock.Of<IStatusStreamLogStore>(),
            Mock.Of<IVpnServerPostSetupService>(),
            Mock.Of<IConnectedClientsCounterStore>(),
            _ovpnConfigQuery.Object,
            new NoOpVpnManagerUpdateStatusEnricher())
        {
            ControllerContext = Context(user)
        };

    private VpnServersV2Controller CreateV2(ClaimsPrincipal user) =>
        new(
            _overviewQuery.Object,
            _serverQuery.Object,
            _quotaGroups.Object,
            _tagQuery.Object,
            _userQuotaPlan.Object,
            _quotaAllowed.Object,
            _accessRules.Object,
            NewCache(),
            _statusCacheGeneration.Object,
            Mock.Of<IConnectedClientsCounterStore>(),
            new NoOpVpnManagerUpdateStatusEnricher())
        {
            ControllerContext = Context(user)
        };

    private VpnServersV3Controller CreateV3(ClaimsPrincipal user) =>
        new(
            _overviewQuery.Object,
            _serverQuery.Object,
            _quotaGroups.Object,
            _tagQuery.Object,
            _groupQuery.Object,
            _userQuotaPlan.Object,
            _quotaAllowed.Object,
            _accessRules.Object,
            _quotaPlanQuery.Object,
            NewCache(),
            _statusCacheGeneration.Object,
            Mock.Of<IConnectedClientsCounterStore>(),
            new NoOpVpnManagerUpdateStatusEnricher())
        {
            ControllerContext = Context(user)
        };

    private static List<VpnServerWithStatusDto> CreateInventory() =>
    [
        Item(TcpOpenVpnId, "Helsinki 1", VpnServerType.OpenVpn),
        Item(UdpOpenVpnId, "Helsinki 2", VpnServerType.OpenVpn),
        Item(XrayId, "Helsinki Xray", VpnServerType.Xray)
    ];

    private static VpnServerWithStatusDto Item(int id, string name, VpnServerType type) => new()
    {
        VpnServerResponses = new VpnServerResponse
        {
            VpnServer = new VpnServerDto
            {
                Id = id,
                ServerName = name,
                ServerType = type,
                IsOnline = true,
                IsEnableWss = true,
                ApiUrl = $"https://{id}.example"
            }
        }
    };

    private static ClaimsPrincipal Admin() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "mock"));

    private static ControllerContext Context(ClaimsPrincipal user) =>
        new() { HttpContext = new DefaultHttpContext { User = user } };

    private static IApiMemoryCacheService NewCache() =>
        new ApiMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    private static JObject ParseLegacyJson(ActionResult actionResult)
    {
        var content = Assert.IsType<ContentResult>(actionResult);
        Assert.False(string.IsNullOrWhiteSpace(content.Content));
        return JObject.Parse(content.Content!);
    }

    private static List<string> NamesFromLegacyV1(JObject json) =>
        json["data"]?["openVpnServerWithStatuses"]?
            .Select(item => item["openVpnServerResponses"]?["openVpnServer"]?["serverName"]?.Value<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList()
        ?? [];

    private static List<string> NamesFromV2(VpnServerWithStatusesV2Response response) =>
        NamesFromV2(response.VpnServerWithStatuses);

    private static List<string> NamesFromV2(List<VpnServerWithStatusV2Dto> items) =>
        items
            .Select(item => item.VpnServerResponses.VpnServer.ServerName)
            .ToList();
}
