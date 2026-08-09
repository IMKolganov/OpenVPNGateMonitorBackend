using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Tests.Helpers;

namespace DataGateMonitor.Tests.DataBase.Services.Query.UserIdentityLinkTable;

public class UserIdentityLinkQueryServiceTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<UserIdentityLink> UserIdentityLinks => Set<UserIdentityLink>();
    }

    private static List<UserIdentityLink> CreateSample() => new()
    {
        new UserIdentityLink { Id = 1, UserId = 100, Provider = "tg", ExternalId = "ext-1" },
        new UserIdentityLink { Id = 2, UserId = 200, Provider = "google", ExternalId = "ext-2" },
        new UserIdentityLink { Id = 3, UserId = 100, Provider = "tg", ExternalId = "ext-3" },
        new UserIdentityLink { Id = 4, UserId = 300, Provider = "tg", ExternalId = "ext-4" },
    };

    private static (Mock<IQueryService<UserIdentityLink, int>> q, TestDbContext ctx) CreateEfBackedQuery(IEnumerable<UserIdentityLink> data)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestDbContext(options);
        ctx.UserIdentityLinks.AddRange(data);
        ctx.SaveChanges();

        var mock = new Mock<IQueryService<UserIdentityLink, int>>();
        mock
            .Setup(q => q.Query(It.IsAny<bool>(), It.IsAny<Expression<Func<UserIdentityLink, object>>[]>()))
            .Returns(ctx.UserIdentityLinks);
        return (mock, ctx);
    }

    [Fact]
    public async Task GetAllAsync_Delegates_To_IQueryService()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        q.Setup(x => x.GetAll(true, It.IsAny<CancellationToken>()))
         .ReturnsAsync(data)
         .Verifiable();

        var sut = new UserIdentityLinkQueryService(q.Object);
        var result = await sut.GetAll(CancellationToken.None);

        Assert.Equal(data.Count, result.Count);
        Assert.True(result.SequenceEqual(data));
        q.Verify(x => x.GetAll(true, It.IsAny<CancellationToken>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_Delegates_To_FindByIdAsync()
    {
        var target = new UserIdentityLink { Id = 42, UserId = 500, Provider = "tg", ExternalId = "E-42" };
        var (q, ctx) = CreateEfBackedQuery(new[] { target });
        q.Setup(x => x.FindById(42, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<UserIdentityLink, object>>[]>()))
         .ReturnsAsync(target)
         .Verifiable();

        var sut = new UserIdentityLinkQueryService(q.Object);
        var result = await sut.GetById(42, CancellationToken.None);

        Assert.Same(target, result);
        q.Verify(x => x.FindById(42, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<UserIdentityLink, object>>[]>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByProviderAndExternalIdAsync_Filters_Via_Query()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByProviderAndExternalId("google", "ext-2", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("google", result!.Provider);
        Assert.Equal("ext-2", result!.ExternalId);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByExternalIdAsync_Filters_Via_Query()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByExternalId("ext-3", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("ext-3", result!.ExternalId);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByUserIdAsync_Filters_Via_Query()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByUserId(100, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(100, result!.UserId);
        Assert.Equal(1, result.Id); // lowest Id when multiple links
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetFirstByUserIds_ReturnsLowestIdLinkPerUser()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetFirstByUserIds([100, 200, 404], CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[100].Id);
        Assert.Equal("ext-1", result[100].ExternalId);
        Assert.Equal(2, result[200].Id);
        Assert.False(result.ContainsKey(404));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetFirstByUserIds_EmptyInput_ReturnsEmpty()
    {
        var (q, ctx) = CreateEfBackedQuery(CreateSample());
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetFirstByUserIds([], CancellationToken.None);

        Assert.Empty(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetListByUserIdAsync_Returns_All_Links_For_User()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetListByUserId(100, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, l => Assert.Equal(100, l.UserId));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetListByUserIdAsync_ReturnsEmpty_WhenNoLinks()
    {
        var (q, ctx) = CreateEfBackedQuery(Array.Empty<UserIdentityLink>());
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetListByUserId(404, CancellationToken.None);

        Assert.Empty(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task AnyByUserIdAsync_Delegates_To_AnyAsync()
    {
        var (q, ctx) = CreateEfBackedQuery(Array.Empty<UserIdentityLink>());
        q.Setup(x => x.Any(It.IsAny<Expression<Func<UserIdentityLink, bool>>>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true)
         .Verifiable();

        var sut = new UserIdentityLinkQueryService(q.Object);
        var found = await sut.AnyByUserId(777, CancellationToken.None);

        Assert.True(found);
        q.Verify(x => x.Any(It.IsAny<Expression<Func<UserIdentityLink, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByProviderAndExternalIdAsync_ReturnsNull_WhenNotFound()
    {
        var (q, ctx) = CreateEfBackedQuery(CreateSample());
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByProviderAndExternalId("missing", "ext", CancellationToken.None);

        Assert.Null(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByExternalIdAsync_ReturnsNull_WhenNotFound()
    {
        var (q, ctx) = CreateEfBackedQuery(CreateSample());
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByExternalId("missing-ext", CancellationToken.None);

        Assert.Null(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsNull_WhenNotFound()
    {
        var (q, ctx) = CreateEfBackedQuery(CreateSample());
        var sut = new UserIdentityLinkQueryService(q.Object);

        var result = await sut.GetByUserId(404, CancellationToken.None);

        Assert.Null(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetPageAsync_Delegates_To_PageAsync()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);

        var paged = new TestPagedResult<UserIdentityLink>
        {
            Page = 1,
            PageSize = 2,
            TotalCount = data.Count,
            Items = data.Take(2).ToList()
        };

        q.Setup(x => x.Page(1, 2, null, null, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<UserIdentityLink, object>>[]>()))
         .ReturnsAsync(paged as IPagedResult<UserIdentityLink>)
         .Verifiable();

        var sut = new UserIdentityLinkQueryService(q.Object);
        var result = await sut.GetPage(1, 2, CancellationToken.None);

        Assert.Same(paged, result);
        q.Verify(x => x.Page(1, 2, null, null, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<UserIdentityLink, object>>[]>()), Times.Once);
        await ctx.DisposeAsync();
    }
}
