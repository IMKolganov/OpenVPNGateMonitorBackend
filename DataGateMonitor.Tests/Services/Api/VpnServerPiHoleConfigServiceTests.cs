using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.DataBase.Repositories;
using DataGateMonitor.DataBase.Repositories.Queries;
using DataGateMonitor.DataBase.Services.Command;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnDnsQueryLogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerPiHoleConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerPiHole.Requests;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnServerPiHoleConfigServiceTests
{
    [Fact]
    public async Task UpsertAsync_CreatesConfig_AndMasksPasswordOnRead()
    {
        await using var harness = await CreateHarnessAsync();
        var sut = harness.Sut;

        var upsert = await sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://pi-hole:8080",
            AppPassword = PiHoleTestFixtures.AppCredential,
            PollIntervalSeconds = 45,
            BatchSize = 300,
            LookbackSeconds = 90,
            ClientSubnetPrefix = "10.51.30."
        }, CancellationToken.None);

        Assert.Equal("http://pi-hole:8080", upsert.Config.BaseUrl);
        Assert.Equal("********", upsert.Config.AppPassword);
        Assert.True(upsert.Config.HasAppPassword);

        var read = await sut.GetForAdminAsync(harness.ServerId, CancellationToken.None);
        Assert.Equal(45, read.Config.PollIntervalSeconds);
        Assert.Equal("10.51.30.", read.Config.ClientSubnetPrefix);
    }

    [Fact]
    public async Task GetRuntimeForMicroserviceAsync_ReturnsNull_WhenPiHoleDisabled()
    {
        await using var harness = await CreateHarnessAsync(isPiHoleEnabled: false);
        var runtime = await harness.Sut.GetRuntimeForMicroserviceAsync(harness.ServerId, CancellationToken.None);
        Assert.Null(runtime);
    }

    [Fact]
    public async Task GetRuntimeForMicroserviceAsync_ReturnsAppCredential_WhenEnabledAndConfigured()
    {
        await using var harness = await CreateHarnessAsync();
        await harness.Sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://pi-hole:8080",
            AppPassword = PiHoleTestFixtures.AppCredential,
            PollIntervalSeconds = 60,
            BatchSize = 200,
            LookbackSeconds = 120,
            ClientSubnetPrefix = ""
        }, CancellationToken.None);

        var runtime = await harness.Sut.GetRuntimeForMicroserviceAsync(harness.ServerId, CancellationToken.None);

        Assert.NotNull(runtime);
        Assert.True(runtime!.IsPiHoleEnabled);
        Assert.Equal(PiHoleTestFixtures.AppCredential, runtime.AppPassword);
    }

    [Fact]
    public async Task UpsertAsync_PreservesPassword_WhenUpdateOmitsAppPassword()
    {
        await using var harness = await CreateHarnessAsync();
        await harness.Sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://pi-hole:8080",
            AppPassword = PiHoleTestFixtures.AppCredential,
            PollIntervalSeconds = 60,
            BatchSize = 200,
            LookbackSeconds = 120,
            ClientSubnetPrefix = ""
        }, CancellationToken.None);

        await harness.Sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://pi-hole:9090",
            AppPassword = null,
            PollIntervalSeconds = 30,
            BatchSize = 150,
            LookbackSeconds = 60,
            ClientSubnetPrefix = "10.51."
        }, CancellationToken.None);

        var runtime = await harness.Sut.GetRuntimeForMicroserviceAsync(harness.ServerId, CancellationToken.None);
        Assert.NotNull(runtime);
        Assert.Equal(PiHoleTestFixtures.AppCredential, runtime!.AppPassword);
        Assert.Equal("http://pi-hole:9090", runtime.BaseUrl);
    }




    private static async Task<Harness> CreateHarnessAsync(
        bool isPiHoleEnabled = true,
        VpnServerType serverType = VpnServerType.OpenVpn,
        HttpMessageHandler? httpHandler = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(b => b.Ignore(RelationalEventId.AmbientTransactionWarning))
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataBaseSettings:DefaultSchema"] = "test_schema" })
            .Build();

        var context = new ApplicationDbContext(options, configuration);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var server = new VpnServer
        {
            ServerName = "pi-hole-test",
            ApiUrl = "http://localhost",
            IsPiHoleEnabled = isPiHoleEnabled,
            ServerType = serverType,
            CreateDate = now,
            LastUpdate = now
        };
        context.VpnServers.Add(server);
        await context.SaveChangesAsync();

        var repositoryFactory = new RepositoryFactory(context);
        var queryFactory = new QueryFactory(context);
        var dbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var unitOfWork = new UnitOfWork(context, dbContextFactory.Object, repositoryFactory, queryFactory);

        var piHoleQuery = new EfQueryService<VpnServerPiHoleConfig, int>(unitOfWork);
        var piHoleCommand = new EfCommandService<VpnServerPiHoleConfig, int>(unitOfWork);
        var piHoleConfigQuery = new VpnServerPiHoleConfigQueryService(piHoleQuery);

        var vpnServerQuery = new Mock<IVpnServerQueryService>();
        vpnServerQuery.Setup(x => x.GetById(server.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server);

        var dnsQuery = new Mock<IVpnDnsQueryLogQueryService>();
        dnsQuery.Setup(x => x.GetServerSummaryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, null));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        if (httpHandler is not null)
        {
            httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(httpHandler, disposeHandler: false));
        }

        var sut = new VpnServerPiHoleConfigService(
            vpnServerQuery.Object,
            piHoleConfigQuery,
            dnsQuery.Object,
            piHoleQuery,
            piHoleCommand,
            httpClientFactory.Object,
            new Mock<IMicroserviceTokenService>().Object,
            NullLogger<VpnServerPiHoleConfigService>.Instance);

        return new Harness(connection, context, server.Id, sut);
    }

    private sealed class Harness(
        SqliteConnection connection,
        ApplicationDbContext context,
        int serverId,
        VpnServerPiHoleConfigService sut) : IAsyncDisposable
    {
        public int ServerId { get; } = serverId;
        public VpnServerPiHoleConfigService Sut { get; } = sut;
        public ApplicationDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
