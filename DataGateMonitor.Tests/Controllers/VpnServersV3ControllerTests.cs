using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.Tests.Services.VpnManagerReleases;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers;

public class VpnServersV3ControllerTests
{
    private readonly Mock<IVpnServerOverviewQuery> _overviewQuery = new();
    private readonly Mock<IVpnServerQueryService> _serverQuery = new();
    private readonly Mock<IVpnServerQuotaPlanGroupsQuery> _quotaGroups = new();
    private readonly Mock<IVpnServerTagQueryService> _tagQuery = new();
    private readonly Mock<IVpnServerGroupQueryService> _groupQuery = new();
    private readonly Mock<IUserQuotaPlanQueryService> _userQuotaPlan = new();
    private readonly Mock<IQuotaPlanAllowedServerQueryService> _quotaAllowed = new();
    private readonly Mock<IUserVpnServerAccessRuleQueryService> _accessRules = new();
    private readonly Mock<IQuotaPlanQueryService> _quotaPlanQuery = new();
    private readonly Mock<IStatusCacheGenerationService> _statusCacheGeneration = new();
    private readonly IApiMemoryCacheService _cache = new ApiMemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

    private VpnServersV3Controller CreateController(ClaimsPrincipal user)
    {
        _groupQuery.Setup(g => g.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _accessRules.Setup(r => r.GetOverridesByUserId(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserVpnServerAccessOverrides.None);

        return new VpnServersV3Controller(
            _overviewQuery.Object,
            _serverQuery.Object,
            _quotaGroups.Object,
            _tagQuery.Object,
            _groupQuery.Object,
            _userQuotaPlan.Object,
            _quotaAllowed.Object,
            _accessRules.Object,
            _quotaPlanQuery.Object,
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
    }

    [Fact]
    public async Task GetAllServers_WhenUserIdMissing_Returns_Unauthorized()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "VpnUser")],
            "mock")));

        var result = await controller.GetAllServers(includeDeleted: false, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV3Response>>(unauthorized.Value);
        Assert.False(api.Success);
    }

    [Fact]
    public async Task GetAllServers_WithVpnUser_ReturnsAllServers_AndMarksAccessibility()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "100")
            ],
            "mock")));

        _userQuotaPlan.Setup(u => u.GetActiveByUserId(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { Id = 5, UserId = 100, QuotaPlanId = 7 });
        _quotaAllowed.Setup(q => q.GetVpnServerIdsByQuotaPlanId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1 });
        _quotaPlanQuery.Setup(q => q.GetById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlan { Id = 7, Name = "Pro" });

        _serverQuery.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VpnServer { Id = 1, ServerName = "allowed" },
                new VpnServer { Id = 2, ServerName = "other" }
            ]);
        _serverQuery.Setup(s => s.GetLastUpdateStamp(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await controller.GetAllServers(false, withoutCache: true, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV3Response>>(ok.Value);
        Assert.True(api.Success);
        Assert.NotNull(api.Data);
        Assert.Equal(2, api.Data!.VpnServers.Count);
        Assert.Equal(7, api.Data.UserQuotaPlan.QuotaPlanId);
        Assert.Equal("Pro", api.Data.UserQuotaPlan.QuotaPlanName);
        Assert.Equal([1], api.Data.UserQuotaPlan.AllowedVpnServerIds);
        Assert.True(api.Data.VpnServers.Single(x => x.Id == 1).IsAccessibleForUserQuotaPlan);
        Assert.False(api.Data.VpnServers.Single(x => x.Id == 2).IsAccessibleForUserQuotaPlan);
        _serverQuery.Verify(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServers_WithVpnUser_DoesNotPassRestrictToQuotaPlanId_ToGetAll()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "51")
            ],
            "mock")));

        _userQuotaPlan.Setup(u => u.GetActiveByUserId(51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { Id = 1, UserId = 51, QuotaPlanId = 9 });
        _quotaAllowed.Setup(q => q.GetVpnServerIdsByQuotaPlanId(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 3 });
        _quotaPlanQuery.Setup(q => q.GetById(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlan { Id = 9, Name = "Basic" });
        _serverQuery.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VpnServer { Id = 3, ServerName = "only-in-full-list" }]);
        _serverQuery.Setup(s => s.GetLastUpdateStamp(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        await controller.GetAllServers(false, withoutCache: true, ct: CancellationToken.None);

        _serverQuery.Verify(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()), Times.Once);
        _serverQuery.Verify(s => s.GetAll(false, false, 9, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllServers_WithPersonalOverrides_MergesIntoContextAndAccessibility()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "VpnUser"),
                new Claim(ClaimTypes.NameIdentifier, "200")
            ],
            "mock")));

        _accessRules.Setup(r => r.GetOverridesByUserId(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVpnServerAccessOverrides([2], [1]));
        _userQuotaPlan.Setup(u => u.GetActiveByUserId(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { Id = 8, UserId = 200, QuotaPlanId = 7 });
        _quotaAllowed.Setup(q => q.GetVpnServerIdsByQuotaPlanId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { 1, 3 });
        _quotaPlanQuery.Setup(q => q.GetById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlan { Id = 7, Name = "Pro" });

        _serverQuery.Setup(s => s.GetAll(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VpnServer { Id = 1, ServerName = "plan-but-denied" },
                new VpnServer { Id = 2, ServerName = "personal-grant" },
                new VpnServer { Id = 3, ServerName = "plan-ok" }
            ]);
        _serverQuery.Setup(s => s.GetLastUpdateStamp(false, false, null, It.IsAny<UserVpnServerAccessOverrides?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);
        _quotaGroups.Setup(g => g.GetGroupsByVpnServerIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<QuotaPlanGroupDto>>());
        _tagQuery.Setup(t => t.GetTagNamesByVpnServerIds(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, List<string>>());

        var result = await controller.GetAllServers(false, withoutCache: true, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<VpnServersV3Response>>(ok.Value);
        Assert.Equal([2, 3], api.Data!.UserQuotaPlan.AllowedVpnServerIds);
        Assert.Equal([2], api.Data.UserQuotaPlan.PersonalAllowedVpnServerIds);
        Assert.Equal([1], api.Data.UserQuotaPlan.PersonalDeniedVpnServerIds);
        Assert.False(api.Data.VpnServers.Single(x => x.Id == 1).IsAccessibleForUserQuotaPlan);
        Assert.True(api.Data.VpnServers.Single(x => x.Id == 2).IsAccessibleForUserQuotaPlan);
        Assert.True(api.Data.VpnServers.Single(x => x.Id == 3).IsAccessibleForUserQuotaPlan);
    }
}
