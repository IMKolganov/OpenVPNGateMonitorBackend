using Microsoft.EntityFrameworkCore;
using Moq;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Tests.Helpers;

namespace DataGateMonitor.Tests.DataBase.Services.Query.VpnServerClientTable;

public class OpenVpnOverviewTotalsQueryTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<VpnServerClient> Clients => Set<VpnServerClient>();
        public DbSet<VpnServerClientTraffic> Traffic => Set<VpnServerClientTraffic>();
        public DbSet<UserIdentityLink> IdentityLinks => Set<UserIdentityLink>();
    }

    private static (Mock<IUnitOfWork> uow, TestDbContext ctx) CreateUow()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestDbContext(options);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.GetQuery<VpnServerClient>())
           .Returns(() => new TestQuery<VpnServerClient>(ctx.Clients));
        uow.Setup(x => x.GetQuery<VpnServerClientTraffic>())
           .Returns(() => new TestQuery<VpnServerClientTraffic>(ctx.Traffic));
        uow.Setup(x => x.GetQuery<UserIdentityLink>())
           .Returns(() => new TestQuery<UserIdentityLink>(ctx.IdentityLinks));
        return (uow, ctx);
    }

    [Fact]
    public async Task GetOverviewTotals_Aggregates_Sessions_Users_And_Traffic_Deltas()
    {
        var (uow, ctx) = CreateUow();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-4);
        var to = now.AddHours(1);

        // Sessions (three within window, two users: e1 & e2)
        ctx.Clients.AddRange(new[]
        {
            new VpnServerClient { Id = 1, VpnServerId = 1, ExternalId = "e1", ConnectedSince = now.AddHours(-3) },
            new VpnServerClient { Id = 2, VpnServerId = 1, ExternalId = "e1", ConnectedSince = now.AddHours(-2) },
            new VpnServerClient { Id = 3, VpnServerId = 1, ExternalId = "e2", ConnectedSince = now.AddHours(-1) },
            // outside window
            new VpnServerClient { Id = 4, VpnServerId = 1, ExternalId = "e3", ConnectedSince = now.AddHours(-10) }
        });

        var sessA = Guid.NewGuid();
        var sessB = Guid.NewGuid();

        // Traffic cumulative samples with deltas: sessA: (0->100 in, 0->50 out) => +100/+50; sessB: (10->30, 5->5) => +20/+0
        ctx.Traffic.AddRange(new[]
        {
            new VpnServerClientTraffic { Id = 1, VpnServerId = 1, ExternalId = "e1", SessionId = sessA, BytesReceived = 0,   BytesSent = 0,  MeasuredAt = now.AddHours(-3) },
            new VpnServerClientTraffic { Id = 2, VpnServerId = 1, ExternalId = "e1", SessionId = sessA, BytesReceived = 100, BytesSent = 50, MeasuredAt = now.AddHours(-2) },
            new VpnServerClientTraffic { Id = 3, VpnServerId = 1, ExternalId = "e2", SessionId = sessB, BytesReceived = 10,  BytesSent = 5,  MeasuredAt = now.AddHours(-1) },
            new VpnServerClientTraffic { Id = 4, VpnServerId = 1, ExternalId = "e2", SessionId = sessB, BytesReceived = 30,  BytesSent = 5,  MeasuredAt = now.AddMinutes(-30) },
        });
        await ctx.SaveChangesAsync();

        var sut = new OpenVpnOverviewTotalsQuery(uow.Object, OverviewQueryTestHelper.CreateTrafficAggregator(uow.Object));
        var res = await sut.GetOverviewTotalsAsync(from, to, vpnServerId: 1, externalId: null, CancellationToken.None);

        Assert.Equal(3, res.Totals.SessionsCount);
        Assert.Equal(2, res.Totals.UsersCount);
        Assert.Equal(0, res.Totals.AccountsCount);
        Assert.Equal(120, res.Totals.TrafficInBytes);
        Assert.Equal(50, res.Totals.TrafficOutBytes);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetOverviewTotals_Filters_By_ExternalId_And_Normalizes_Range()
    {
        var (uow, ctx) = CreateUow();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-4);
        var to = now.AddHours(1);

        ctx.Clients.AddRange(new[]
        {
            new VpnServerClient { Id = 1, VpnServerId = 2, ExternalId = "X", ConnectedSince = now.AddHours(-3) },
            new VpnServerClient { Id = 2, VpnServerId = 2, ExternalId = "Y", ConnectedSince = now.AddHours(-2) }
        });

        var s = Guid.NewGuid();
        ctx.Traffic.AddRange(new[]
        {
            new VpnServerClientTraffic { Id = 1, VpnServerId = 2, ExternalId = "X", SessionId = s, BytesReceived = 1, BytesSent = 1, MeasuredAt = now.AddHours(-3) },
            new VpnServerClientTraffic { Id = 2, VpnServerId = 2, ExternalId = "X", SessionId = s, BytesReceived = 6, BytesSent = 4, MeasuredAt = now.AddHours(-2) },
        });
        await ctx.SaveChangesAsync();

        var sut = new OpenVpnOverviewTotalsQuery(uow.Object, OverviewQueryTestHelper.CreateTrafficAggregator(uow.Object));
        // reversed bounds, filtered by externalId
        var res = await sut.GetOverviewTotalsAsync(to, from, vpnServerId: 2, externalId: "X", CancellationToken.None);

        Assert.Equal(1, res.Totals.SessionsCount);
        Assert.Equal(1, res.Totals.UsersCount);
        Assert.Equal(0, res.Totals.AccountsCount);
        Assert.Equal(5, res.Totals.TrafficInBytes);
        Assert.Equal(3, res.Totals.TrafficOutBytes);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetOverviewTotals_Uses_PreWindow_Baseline_For_First_Sample_In_Window()
    {
        var (uow, ctx) = CreateUow();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-2);
        var to = now.AddHours(1);

        ctx.Clients.Add(new VpnServerClient
        {
            Id = 1,
            VpnServerId = 7,
            ExternalId = "xr-1",
            ConnectedSince = now.AddHours(-1),
        });

        var s = Guid.NewGuid();
        ctx.Traffic.AddRange(new[]
        {
            // Baseline before range start
            new VpnServerClientTraffic { Id = 1, VpnServerId = 7, ExternalId = "xr-1", SessionId = s, BytesReceived = 1000, BytesSent = 300, MeasuredAt = now.AddHours(-3) },
            // Only one sample in [from;to)
            new VpnServerClientTraffic { Id = 2, VpnServerId = 7, ExternalId = "xr-1", SessionId = s, BytesReceived = 1400, BytesSent = 500, MeasuredAt = now.AddHours(-1) },
        });
        await ctx.SaveChangesAsync();

        var sut = new OpenVpnOverviewTotalsQuery(uow.Object, OverviewQueryTestHelper.CreateTrafficAggregator(uow.Object));
        var res = await sut.GetOverviewTotalsAsync(from, to, vpnServerId: 7, externalId: null, CancellationToken.None);

        Assert.Equal(400, res.Totals.TrafficInBytes);
        Assert.Equal(200, res.Totals.TrafficOutBytes);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetOverviewTotals_Counts_Distinct_Accounts_From_IdentityLinks()
    {
        var (uow, ctx) = CreateUow();
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-4);
        var to = now.AddHours(1);

        ctx.Clients.AddRange(new[]
        {
            new VpnServerClient { Id = 1, VpnServerId = 1, ExternalId = "dev-a", ConnectedSince = now.AddHours(-3) },
            new VpnServerClient { Id = 2, VpnServerId = 1, ExternalId = "dev-b", ConnectedSince = now.AddHours(-2) },
            new VpnServerClient { Id = 3, VpnServerId = 1, ExternalId = "dev-c", UserId = 42, ConnectedSince = now.AddHours(-1) },
        });
        ctx.IdentityLinks.AddRange(new[]
        {
            new UserIdentityLink { Id = 1, UserId = 7, Provider = "google", ExternalId = "dev-a" },
            new UserIdentityLink { Id = 2, UserId = 7, Provider = "telegram", ExternalId = "dev-b" },
        });
        await ctx.SaveChangesAsync();

        var sut = new OpenVpnOverviewTotalsQuery(uow.Object, OverviewQueryTestHelper.CreateTrafficAggregator(uow.Object));
        var res = await sut.GetOverviewTotalsAsync(from, to, vpnServerId: 1, externalId: null, CancellationToken.None);

        Assert.Equal(3, res.Totals.SessionsCount);
        Assert.Equal(3, res.Totals.UsersCount);
        Assert.Equal(2, res.Totals.AccountsCount);

        await ctx.DisposeAsync();
    }
}
