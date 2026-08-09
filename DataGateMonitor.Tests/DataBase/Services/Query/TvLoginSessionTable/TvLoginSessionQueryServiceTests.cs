using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.Models;
using Xunit;

namespace DataGateMonitor.Tests.DataBase.Services.Query.TvLoginSessionTable;

public class TvLoginSessionQueryServiceTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<TvLoginSession> TvLoginSessions => Set<TvLoginSession>();
    }

    private static (Mock<IQueryService<TvLoginSession, Guid>> q, TestDbContext ctx) CreateEfBackedQuery(
        IEnumerable<TvLoginSession> data)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestDbContext(options);
        ctx.TvLoginSessions.AddRange(data);
        ctx.SaveChanges();

        var mock = new Mock<IQueryService<TvLoginSession, Guid>>();
        mock.Setup(q => q.Query(It.IsAny<bool>(), It.IsAny<Expression<Func<TvLoginSession, object>>[]>()))
            .Returns(ctx.TvLoginSessions.AsQueryable());
        mock.Setup(q => q.FindById(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<TvLoginSession, object>>[]>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken __, Expression<Func<TvLoginSession, object>>[] ___) =>
                ctx.TvLoginSessions.FirstOrDefault(x => x.Id == id));
        return (mock, ctx);
    }

    [Fact]
    public async Task GetById_ReturnsEntity()
    {
        var id = Guid.NewGuid();
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = id,
                UserCode = "123456",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            }
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        var result = await sut.GetById(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("123456", result!.UserCode);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveByUserCode_ReturnsPendingOrViewed_NotExpiredOrConsumed()
    {
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = Guid.NewGuid(),
                UserCode = "111111",
                Status = TvLoginSessionStatus.Consumed,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                CreateDate = DateTimeOffset.UtcNow.AddMinutes(-2),
            },
            new TvLoginSession
            {
                Id = Guid.NewGuid(),
                UserCode = "111111",
                Status = TvLoginSessionStatus.Viewed,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                CreateDate = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            new TvLoginSession
            {
                Id = Guid.NewGuid(),
                UserCode = "111111",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreateDate = DateTimeOffset.UtcNow,
            },
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        var active = await sut.GetActiveByUserCode("111111", CancellationToken.None);

        Assert.NotNull(active);
        Assert.Equal(TvLoginSessionStatus.Viewed, active!.Status);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task AnyActiveByUserCode_TrueOnlyForOpenNonExpired()
    {
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = Guid.NewGuid(),
                UserCode = "222222",
                Status = TvLoginSessionStatus.Denied,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            }
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        Assert.False(await sut.AnyActiveByUserCode("222222", CancellationToken.None));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetLatestByUserCode_ReturnsNewestRegardlessOfStatus()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = older,
                UserCode = "333333",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                CreateDate = DateTimeOffset.UtcNow.AddMinutes(-10),
            },
            new TvLoginSession
            {
                Id = newer,
                UserCode = "333333",
                Status = TvLoginSessionStatus.Expired,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreateDate = DateTimeOffset.UtcNow,
            },
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        var latest = await sut.GetLatestByUserCode("333333", CancellationToken.None);

        Assert.Equal(newer, latest!.Id);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNull()
    {
        var (q, ctx) = CreateEfBackedQuery([]);
        var sut = new TvLoginSessionQueryService(q.Object);

        Assert.Null(await sut.GetById(Guid.NewGuid(), CancellationToken.None));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task AnyActiveByUserCode_TrueForOpenPending()
    {
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = Guid.NewGuid(),
                UserCode = "444444",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            }
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        Assert.True(await sut.AnyActiveByUserCode("444444", CancellationToken.None));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveByUserCode_WhenTwoOpen_ReturnsNewest()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var (q, ctx) = CreateEfBackedQuery(
        [
            new TvLoginSession
            {
                Id = older,
                UserCode = "555555",
                Status = TvLoginSessionStatus.Pending,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                CreateDate = DateTimeOffset.UtcNow.AddMinutes(-5),
            },
            new TvLoginSession
            {
                Id = newer,
                UserCode = "555555",
                Status = TvLoginSessionStatus.Viewed,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                CreateDate = DateTimeOffset.UtcNow,
            },
        ]);
        var sut = new TvLoginSessionQueryService(q.Object);

        var active = await sut.GetActiveByUserCode("555555", CancellationToken.None);

        Assert.Equal(newer, active!.Id);
        await ctx.DisposeAsync();
    }
}
