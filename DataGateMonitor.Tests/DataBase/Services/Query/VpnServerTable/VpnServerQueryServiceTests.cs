using Moq;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Tests.Helpers;
using Xunit;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerTable;

public class VpnServerQueryServiceTests
{
    [Fact]
    public async Task GetAll_WhenIncludeDeletedFalse_CallsWhereWithNotIsDeleted()
    {
        var list = new List<VpnServer> { new() { Id = 1, ServerName = "S1", IsDeleted = false } };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, true, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync(list)
            .Verifiable();

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetAll(includeDeleted: false, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null, personalOverrides: null, CancellationToken.None);

        Assert.Single(result);
        Assert.False(result[0].IsDeleted);
        q.Verify(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, true, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenIncludeDeletedTrue_CallsGetAll()
    {
        var list = new List<VpnServer>
        {
            new() { Id = 1, ServerName = "S1", IsDeleted = false },
            new() { Id = 2, ServerName = "S2", IsDeleted = true }
        };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.GetAll(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetAll(includeDeleted: true, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null, personalOverrides: null, CancellationToken.None);

        Assert.Equal(2, result.Count);
        q.Verify(x => x.GetAll(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_DelegatesToFindById()
    {
        var server = new VpnServer { Id = 10, ServerName = "S" };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.FindById(10, It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync(server);

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetById(10, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10, result!.Id);
    }

    [Fact]
    public async Task GetDefaultExcept_DelegatesToWhere_ExcludingDeletedAndExceptId()
    {
        var list = new List<VpnServer> { new() { Id = 2, ServerName = "D", IsDefault = true, IsDeleted = false } };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, true, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync(list);

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetDefaultExcept(5, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
        Assert.True(result[0].IsDefault);
        q.Verify(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, true, It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()), Times.Once);
    }

    [Fact]
    public async Task GetPage_WhenIncludeDeletedFalse_CallsPageWithPredicate()
    {
        var paged = new TestPagedResult<VpnServer> { Page = 1, PageSize = 10, TotalCount = 1, Items = [new VpnServer { Id = 1, IsDeleted = false }] };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.Page(1, 10, It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync(paged);

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetPage(1, 10, includeDeleted: false, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null, personalOverrides: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        q.Verify(x => x.Page(1, 10, It.IsNotNull<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()), Times.Once);
    }

    [Fact]
    public async Task GetPage_WhenIncludeDeletedTrue_CallsPageWithoutPredicate()
    {
        var paged = new TestPagedResult<VpnServer> { Page = 1, PageSize = 10, TotalCount = 2, Items = [new VpnServer { Id = 1 }, new VpnServer { Id = 2, IsDeleted = true }] };
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.Page(1, 10, null, null, It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync(paged);

        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);
        var result = await sut.GetPage(1, 10, includeDeleted: true, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null, personalOverrides: null, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        q.Verify(x => x.Page(1, 10, null, null, It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithQuotaPlan_AddsPersonallyAllowedServer_AndDropsBlockedOne()
    {
        var servers = new List<VpnServer>
        {
            new() { Id = 1, ServerName = "PlanServer" },
            new() { Id = 2, ServerName = "PlanServerBlocked" },
            new() { Id = 3, ServerName = "PersonalGrant" },
            new() { Id = 4, ServerName = "Unrelated" }
        };

        var q = CreateQueryMatching(servers);
        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        allowed.Setup(a => a.GetVpnServerIdsByQuotaPlanId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2]);

        var overrides = new UserVpnServerAccessOverrides([3], [2]);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);

        var result = await sut.GetAll(includeDeleted: false, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: 7, overrides, CancellationToken.None);

        Assert.Equal([1, 3], result.Select(s => s.Id).Order());
    }

    [Fact]
    public async Task GetAll_WithoutQuotaPlan_StillHidesPersonallyBlockedServer()
    {
        var servers = new List<VpnServer>
        {
            new() { Id = 1, ServerName = "S1" },
            new() { Id = 2, ServerName = "Blocked" }
        };

        var q = CreateQueryMatching(servers);
        var allowed = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        var overrides = new UserVpnServerAccessOverrides([], [2]);
        var sut = new VpnServerQueryService(q.Object, allowed.Object);

        var result = await sut.GetAll(includeDeleted: false, requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null, overrides, CancellationToken.None);

        Assert.Equal([1], result.Select(s => s.Id));
    }

    /// <summary>Applies the predicate the service builds to <paramref name="servers"/> so the filtering itself is asserted.</summary>
    private static Mock<IQueryService<VpnServer, int>> CreateQueryMatching(List<VpnServer> servers)
    {
        var q = new Mock<IQueryService<VpnServer, int>>();
        q.Setup(x => x.Where(
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<VpnServer, object>>[]>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<VpnServer, bool>> predicate,
                    System.Linq.Expressions.Expression<Func<VpnServer, object>>? _,
                    bool __,
                    CancellationToken ___,
                    System.Linq.Expressions.Expression<Func<VpnServer, object>>[] ____) =>
                servers.Where(predicate.Compile()).ToList());
        return q;
    }
}
