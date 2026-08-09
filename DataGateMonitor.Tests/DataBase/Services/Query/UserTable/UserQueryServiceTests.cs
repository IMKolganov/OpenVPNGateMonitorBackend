using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;

namespace DataGateMonitor.Tests.DataBase.Services.Query.UserTable;

public partial class UserQueryServiceTests
{
    private static DbContextOptions<TestDbContext> CreateOptions()
        => new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static (UserQueryService sut, TestDbContext ctx) CreateSutWithContext()
    {
        var ctx = new TestDbContext(CreateOptions());
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.GetQuery<User>())
            .Returns(new TestQuery<User>(ctx.Users));
        uow.Setup(u => u.GetQuery<UserIdentityLink>())
            .Returns(new TestQuery<UserIdentityLink>(ctx.UserIdentityLinks));

        var qUser = new EfQueryService<User, int>(uow.Object);
        var qLink = new EfQueryService<UserIdentityLink, int>(uow.Object);
        var sut = new UserQueryService(qUser, qLink, uow.Object);
        return (sut, ctx);
    }

    [Fact]
    public async Task GetByExternalId_Returns_User_When_Link_Exists()
    {
        var (sut, ctx) = CreateSutWithContext();
        await ctx.Users.AddRangeAsync(
            new User { Id = 1, DisplayName = "u1" },
            new User { Id = 2, DisplayName = "u2" }
        );
        await ctx.UserIdentityLinks.AddRangeAsync(
            new UserIdentityLink { Id = 10, Provider = "tg", ExternalId = "ext-1", UserId = 2 }
        );
        await ctx.SaveChangesAsync();

        var user = await sut.GetByExternalId("ext-1", CancellationToken.None);
        Assert.NotNull(user);
        Assert.Equal(2, user!.Id);
        Assert.Equal("u2", user.DisplayName);
    }

    [Fact]
    public async Task GetByExternalId_Returns_Null_When_No_Link()
    {
        var (sut, ctx) = CreateSutWithContext();
        await ctx.Users.AddAsync(new User { Id = 1, DisplayName = "u1" });
        await ctx.SaveChangesAsync();

        var user = await sut.GetByExternalId("missing", CancellationToken.None);
        Assert.Null(user);
    }

    [Fact]
    public async Task GetByExternalIds_Batches_And_Picks_Lowest_Link_Id_Per_ExternalId()
    {
        var (sut, ctx) = CreateSutWithContext();
        await ctx.Users.AddRangeAsync(
            new User { Id = 1, DisplayName = "first", AvatarUrl = "a1" },
            new User { Id = 2, DisplayName = "second", AvatarUrl = "a2" },
            new User { Id = 3, DisplayName = "third" }
        );
        await ctx.UserIdentityLinks.AddRangeAsync(
            new UserIdentityLink { Id = 20, Provider = "tg", ExternalId = "ext-dup", UserId = 2 },
            new UserIdentityLink { Id = 10, Provider = "google", ExternalId = "ext-dup", UserId = 1 },
            new UserIdentityLink { Id = 30, Provider = "tg", ExternalId = "ext-only", UserId = 3 }
        );
        await ctx.SaveChangesAsync();

        var map = await sut.GetByExternalIds(["ext-dup", "ext-only", "missing", ""], CancellationToken.None);

        Assert.Equal(2, map.Count);
        Assert.Equal(1, map["ext-dup"].Id);
        Assert.Equal("first", map["ext-dup"].DisplayName);
        Assert.Equal(3, map["ext-only"].Id);
        Assert.False(map.ContainsKey("missing"));
    }

    [Fact]
    public async Task GetByExternalIds_Empty_Input_Returns_Empty_Map()
    {
        var (sut, _) = CreateSutWithContext();
        var map = await sut.GetByExternalIds([], CancellationToken.None);
        Assert.Empty(map);
    }

    [Fact]
    public async Task Paging_Works_With_Desc_Sort()
    {
        var (sut, ctx) = CreateSutWithContext();
        await ctx.Users.AddRangeAsync(Enumerable.Range(1, 15).Select(i => new User { Id = i, DisplayName = $"u{i}" }));
        await ctx.SaveChangesAsync();

        var page = await sut.GetPage(2, 10, CancellationToken.None);
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(15, page.TotalCount);
        Assert.Equal(5, page.Items.Count);
        // При сортировке DESC: 15, 14, 13, 12, 11, 10, 9, 8, 7, 6 (страница 1)
        // 5, 4, 3, 2, 1 (страница 2)
        Assert.Equal(5, page.Items.First().Id);
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<UserIdentityLink> UserIdentityLinks => Set<UserIdentityLink>();
    }
}
