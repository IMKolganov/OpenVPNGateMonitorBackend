using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.Tests.Services.VpnManagerReleases;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers;

/// <summary>
/// Legacy Android compatibility regression tests for VPN server listing.
/// Keep these expectations aligned with old Android client behavior.
/// </summary>
public class VpnServersV2ControllerTests
{
    private readonly Mock<IVpnServerOverviewQuery> _overviewQuery = new();
    private readonly Mock<IVpnServerQueryService> _serverQuery = new();
    private readonly Mock<IVpnServerQuotaPlanGroupsQuery> _quotaGroups = new();
    private readonly Mock<IVpnServerTagQueryService> _tagQuery = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IQuotaPlanAllowedServerQueryService> _quotaAllowed = new();
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGeneration = new();
    private readonly IApiMemoryCacheService _cache = new ApiMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    private VpnServersV2Controller CreateController(ClaimsPrincipal user)
    {
        var c = new VpnServersV2Controller(
            _overviewQuery.Object,
            _serverQuery.Object,
            _quotaGroups.Object,
            _tagQuery.Object,
            _userQuotaPlan.Object,
            _quotaAllowed.Object,
            _cache,
            _statusCacheGeneration.Object,
            Mock.Of<IConnectedClientsCounterStore>(),
            new NoOpVpnManagerUpdateStatusEnricher())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
        return c;
    }

    [Fact]
    public async Task GetAllServers_WhenUserIdMissing_Returns_Unauthorized()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "VpnUser")],
            "mock"));
        var controller = CreateController(user);

        _serverQuery.Setup(s => s.GetAll(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VpnServer { Id = 1, ServerName = "a" }]);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await controller.GetAllServers(includeDeleted: false, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV2Response>>(unauthorized.Value);
        Assert.False(api.Success);
    }

    [Fact]
    public async Task GetAllServers_WithVpnUser_MarksAccessibility_FromQuotaPlan()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "100")
            ],
            "mock"));
        var controller = CreateController(user);

        _userQuotaPlan.Setup(u => u.GetActiveByUserId(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { Id = 1, UserId = 100, QuotaPlanId = 7 });
        _quotaAllowed.Setup(q => q.GetVpnServerIdsByQuotaPlanId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1 });

        _serverQuery.Setup(s => s.GetAll(false, false, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VpnServer { Id = 1, ServerName = "allowed" },
                new VpnServer { Id = 2, ServerName = "other" }
            ]);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await controller.GetAllServers(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV2Response>>(ok.Value);
        Assert.True(api.Success);
        Assert.NotNull(api.Data);
        Assert.Equal(2, api.Data!.VpnServers.Count);
        var s1 = api.Data.VpnServers.Single(x => x.Id == 1);
        var s2 = api.Data.VpnServers.Single(x => x.Id == 2);
        Assert.True(s1.IsAccessibleForUserQuotaPlan);
        Assert.False(s2.IsAccessibleForUserQuotaPlan);
    }

    [Fact]
    [Trait("Compatibility", "LegacyAndroid")]
    public async Task GetAllServers_WithVpnUserAndNoQuotaPlan_StillMarksServersAccessible_ForLegacyAndroidCompatibility()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "101")
            ],
            "mock"));
        var controller = CreateController(user);

        _userQuotaPlan.Setup(u => u.GetActiveByUserId(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserQuotaPlan?)null);

        _serverQuery.Setup(s => s.GetAll(false, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VpnServer { Id = 1, ServerName = "legacy-a" },
                new VpnServer { Id = 2, ServerName = "legacy-b" }
            ]);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await controller.GetAllServers(false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV2Response>>(ok.Value);
        Assert.True(api.Success);
        Assert.NotNull(api.Data);
        Assert.Equal(2, api.Data!.VpnServers.Count);
        Assert.All(api.Data.VpnServers, s => Assert.True(s.IsAccessibleForUserQuotaPlan));
    }

    [Fact]
    public async Task GetAllServersWithStatus_WhenUserIdMissing_Returns_Unauthorized()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "VpnUser")],
            "mock"));
        var controller = CreateController(user);

        _overviewQuery.Setup(o => o.GetAllVpnServersWithStatusAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VpnServerWithStatusDto
                {
                    VpnServerResponses = new VpnServerResponse
                    {
                        VpnServer = new VpnServerDto { Id = 1, ServerName = "x" }
                    }
                }
            ]);

        var result = await controller.GetAllServersWithStatus(false, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServerWithStatusesV2Response>>(unauthorized.Value);
        Assert.False(api.Success);
    }
}
