using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Tests.Helpers;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerOvpnFileConfigTable;

public class VpnServerOvpnFileConfigQueryServiceTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<VpnServerOvpnFileConfig> VpnServerOvpnFileConfigs => Set<VpnServerOvpnFileConfig>();
    }

    private static List<VpnServerOvpnFileConfig> CreateSample() => new()
    {
        new VpnServerOvpnFileConfig { Id = 1, VpnServerId = 11, VpnServerIp = "10.0.0.1", VpnServerPort = 1194 },
        new VpnServerOvpnFileConfig { Id = 2, VpnServerId = 22, VpnServerIp = "10.0.0.2", VpnServerPort = 1194 },
        new VpnServerOvpnFileConfig { Id = 3, VpnServerId = 33, VpnServerIp = "10.0.0.3", VpnServerPort = 1194 },
    };

    private static (Mock<IQueryService<VpnServerOvpnFileConfig, int>> q, TestDbContext ctx) CreateEfBackedQuery(IEnumerable<VpnServerOvpnFileConfig> data)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestDbContext(options);
        ctx.VpnServerOvpnFileConfigs.AddRange(data);
        ctx.SaveChanges();

        var mock = new Mock<IQueryService<VpnServerOvpnFileConfig, int>>();
        mock
            .Setup(q => q.Query(It.IsAny<bool>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()))
            .Returns(ctx.VpnServerOvpnFileConfigs);
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

        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);
        var result = await sut.GetAll(CancellationToken.None);

        Assert.Equal(data.Count, result.Count);
        Assert.True(result.SequenceEqual(data));
        q.Verify(x => x.GetAll(true, It.IsAny<CancellationToken>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_Delegates_To_FindByIdAsync()
    {
        var target = new VpnServerOvpnFileConfig { Id = 42, VpnServerId = 777, VpnServerIp = "10.8.0.1" };
        var (q, ctx) = CreateEfBackedQuery(new[] { target });
        q.Setup(x => x.FindById(42, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()))
         .ReturnsAsync(target)
         .Verifiable();

        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);
        var result = await sut.GetById(42, CancellationToken.None);

        Assert.Same(target, result);
        q.Verify(x => x.FindById(42, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetByVpnServerIdIdAsync_Uses_Query_To_Return_Matching_Record()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);

        var result = await sut.GetByVpnServerIdId(22, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(22, result!.VpnServerId);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetConfigTemplatesByVpnServerIds_ReturnsTemplatesForRequestedServers()
    {
        var data = CreateSample();
        data[0].ConfigTemplate = "proto tcp";
        data[1].ConfigTemplate = "proto udp";
        data[2].ConfigTemplate = "proto tcp";
        var (q, ctx) = CreateEfBackedQuery(data);
        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);

        var result = await sut.GetConfigTemplatesByVpnServerIds([11, 22], CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("proto tcp", result[11]);
        Assert.Equal("proto udp", result[22]);
        Assert.False(result.ContainsKey(33));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetConfigTemplatesByVpnServerIds_WhenEmpty_DoesNotQuery()
    {
        var (q, ctx) = CreateEfBackedQuery(CreateSample());
        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);

        var result = await sut.GetConfigTemplatesByVpnServerIds([], CancellationToken.None);

        Assert.Empty(result);
        q.Verify(
            x => x.Query(It.IsAny<bool>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()),
            Times.Never);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task AnyByVpnServerId_Delegates_To_AnyAsync()
    {
        var (q, ctx) = CreateEfBackedQuery(Array.Empty<VpnServerOvpnFileConfig>());
        q.Setup(x => x.Any(It.IsAny<Expression<Func<VpnServerOvpnFileConfig, bool>>>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(true)
         .Verifiable();

        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);
        var found = await sut.AnyByVpnServerId(999, CancellationToken.None);

        Assert.True(found);
        q.Verify(x => x.Any(It.IsAny<Expression<Func<VpnServerOvpnFileConfig, bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetPageAsync_Delegates_To_PageAsync()
    {
        var data = CreateSample();
        var (q, ctx) = CreateEfBackedQuery(data);

        var paged = new TestPagedResult<VpnServerOvpnFileConfig>
        {
            Page = 1,
            PageSize = 2,
            TotalCount = data.Count,
            Items = data.Take(2).ToList()
        };

        q.Setup(x => x.Page(1, 2, null, null, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()))
         .ReturnsAsync(paged as IPagedResult<VpnServerOvpnFileConfig>)
         .Verifiable();

        var sut = new VpnServerOvpnFileConfigQueryService(q.Object);
        var result = await sut.GetPage(1, 2, CancellationToken.None);

        Assert.Same(paged, result);
        q.Verify(x => x.Page(1, 2, null, null, true, It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<VpnServerOvpnFileConfig, object>>[]>()), Times.Once);
        await ctx.DisposeAsync();
    }
}
