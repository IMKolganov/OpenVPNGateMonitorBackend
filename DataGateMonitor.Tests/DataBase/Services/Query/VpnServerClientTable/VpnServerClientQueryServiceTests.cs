using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Tests.Helpers;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerClientTable;

public class VpnServerClientQueryServiceTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<VpnServerClient> Clients => Set<VpnServerClient>();
    }

    private static (Mock<IQueryService<VpnServerClient, int>> q, TestDbContext ctx) CreateEfBackedQuery(IEnumerable<VpnServerClient> data)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestDbContext(options);
        ctx.Clients.AddRange(data);
        ctx.SaveChanges();

        var mock = new Mock<IQueryService<VpnServerClient, int>>();
        mock
            .Setup(x => x.Query(It.IsAny<bool>(), It.IsAny<Expression<Func<VpnServerClient, object>>[]>()))
            .Returns(ctx.Clients);
        return (mock, ctx);
    }

    private static List<VpnServerClient> Sample() => new()
    {
        new VpnServerClient { Id = 1, VpnServerId = 10, IsConnected = true,  SessionId = Guid.NewGuid(), ExternalId = "u1", ConnectedSince = DateTimeOffset.UtcNow.AddHours(-3) },
        new VpnServerClient { Id = 2, VpnServerId = 10, IsConnected = false, SessionId = Guid.NewGuid(), ExternalId = "u2", ConnectedSince = DateTimeOffset.UtcNow.AddHours(-2) },
        new VpnServerClient { Id = 3, VpnServerId = 20, IsConnected = true,  SessionId = Guid.NewGuid(), ExternalId = "u1", ConnectedSince = DateTimeOffset.UtcNow.AddHours(-1) },
    };

    [Fact]
    public async Task GetAllAsync_Delegates()
    {
        var data = Sample();
        var (q, ctx) = CreateEfBackedQuery(data);

        q.Setup(x => x.GetAll(true, It.IsAny<CancellationToken>()))
         .ReturnsAsync(data)
         .Verifiable();

        var sut = new VpnServerClientQueryService(q.Object);
        var result = await sut.GetAll(CancellationToken.None);

        Assert.Equal(3, result.Count);
        q.Verify();
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetAllConnectedByVpnServerIdAsync_Uses_WhereAsync_With_Filter()
    {
        var data = Sample();
        var (q, ctx) = CreateEfBackedQuery(data);

        Expression<Func<VpnServerClient, bool>>? captured = null;

        q.Setup(x => x.Where(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<VpnServerClient, object>>[]>()
            ))
         .Callback((Expression<Func<VpnServerClient, bool>> predicate,
             Func<IQueryable<VpnServerClient>, IOrderedQueryable<VpnServerClient>>? orderBy,
             bool _, CancellationToken __, Expression<Func<VpnServerClient, object>>[] ___) =>
         {
             captured = predicate;
         })
         .ReturnsAsync(data.Where(x => x.VpnServerId == 10 && x.IsConnected).ToList())
         .Verifiable();

        var sut = new VpnServerClientQueryService(q.Object);
        var result = await sut.GetAllConnectedByVpnServerId(10, CancellationToken.None);

        Assert.All(result, x => { Assert.Equal(10, x.VpnServerId); Assert.True(x.IsConnected); });
        Assert.NotNull(captured);
        var filtered = ctx.Clients.Where(captured!).ToList();
        Assert.All(filtered, x => { Assert.Equal(10, x.VpnServerId); Assert.True(x.IsConnected); });
        q.Verify();
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetAllConnectedAsync_Uses_WhereAsync_With_IsConnectedFilter()
    {
        var data = Sample();
        var (q, ctx) = CreateEfBackedQuery(data);

        Expression<Func<VpnServerClient, bool>>? captured = null;

        q.Setup(x => x.Where(
                It.IsAny<Expression<Func<VpnServerClient, bool>>>(),
                null,
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<VpnServerClient, object>>[]>()
            ))
         .Callback((Expression<Func<VpnServerClient, bool>> predicate,
             Func<IQueryable<VpnServerClient>, IOrderedQueryable<VpnServerClient>>? _,
             bool __, CancellationToken ___, Expression<Func<VpnServerClient, object>>[] ____) =>
         {
             captured = predicate;
         })
         .ReturnsAsync(data.Where(x => x.IsConnected).ToList())
         .Verifiable();

        var sut = new VpnServerClientQueryService(q.Object);
        var result = await sut.GetAllConnected(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.IsConnected));
        Assert.NotNull(captured);
        var filtered = ctx.Clients.Where(captured!).ToList();
        Assert.All(filtered, x => Assert.True(x.IsConnected));
        q.Verify();
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetConnectedVpnServerIdsForUserAsync_MatchesByUserIdOrExternalId()
    {
        var data = new List<VpnServerClient>
        {
            new() { Id = 1, VpnServerId = 10, UserId = 7, ExternalId = "u1", IsConnected = true, SessionId = Guid.NewGuid(), ConnectedSince = DateTimeOffset.UtcNow },
            new() { Id = 2, VpnServerId = 20, UserId = 99, ExternalId = "u1", IsConnected = true, SessionId = Guid.NewGuid(), ConnectedSince = DateTimeOffset.UtcNow },
            new() { Id = 3, VpnServerId = 30, UserId = 7, ExternalId = "u2", IsConnected = false, SessionId = Guid.NewGuid(), ConnectedSince = DateTimeOffset.UtcNow },
            new() { Id = 4, VpnServerId = 40, UserId = 8, ExternalId = "u3", IsConnected = true, SessionId = Guid.NewGuid(), ConnectedSince = DateTimeOffset.UtcNow },
        };

        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new VpnServerClientQueryService(q.Object);

        var result = await sut.GetConnectedVpnServerIdsForUserAsync(7, "u1", CancellationToken.None);

        Assert.Equal(new[] { 10, 20 }, result.OrderBy(x => x));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetConnectedVpnServerIdsForUserAsync_ReturnsEmpty_WhenIdentityMissing()
    {
        var (q, ctx) = CreateEfBackedQuery(Sample());
        var sut = new VpnServerClientQueryService(q.Object);

        var result = await sut.GetConnectedVpnServerIdsForUserAsync(null, null, CancellationToken.None);

        Assert.Empty(result);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_Delegates()
    {
        var entity = new VpnServerClient { Id = 42, VpnServerId = 10, IsConnected = true, SessionId = Guid.NewGuid(), ConnectedSince = DateTimeOffset.UtcNow };
        var (q, ctx) = CreateEfBackedQuery(new[] { entity });

        q.Setup(x => x.FindById(42, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerClient, object>>[]>()))
         .ReturnsAsync(entity)
         .Verifiable();

        var sut = new VpnServerClientQueryService(q.Object);
        var res = await sut.GetById(42, CancellationToken.None);
        Assert.Same(entity, res);
        q.Verify();
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetBySessionAndServerIdAsync_Queries_DbSet()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var data = new List<VpnServerClient>
        {
            new() { Id = 1, VpnServerId = 10, SessionId = s1, IsConnected = true, ConnectedSince = DateTimeOffset.UtcNow.AddHours(-2) },
            new() { Id = 2, VpnServerId = 10, SessionId = s2, IsConnected = true, ConnectedSince = DateTimeOffset.UtcNow.AddHours(-1) },
        };

        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new VpnServerClientQueryService(q.Object);

        var found = await sut.GetBySessionAndServerId(s2, 10, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(2, found!.Id);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetPageAsync_Delegates()
    {
        var data = Sample();
        var (q, ctx) = CreateEfBackedQuery(data);

        var paged = new TestPagedResult<VpnServerClient>
        {
            Page = 1,
            PageSize = 2,
            TotalCount = data.Count,
            Items = data.Take(2).ToList()
        };

        q.Setup(x => x.Page(1, 2, null, null, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerClient, object>>[]>()))
         .ReturnsAsync(paged as IPagedResult<VpnServerClient>)
         .Verifiable();

        var sut = new VpnServerClientQueryService(q.Object);
        var res = await sut.GetPage(1, 2, CancellationToken.None);
        Assert.Same(paged, res);
        q.Verify();
        await ctx.DisposeAsync();
    }
}
