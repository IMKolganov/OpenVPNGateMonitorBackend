using System.Net;
using System.Net.Http.Headers;
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
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.Diagnostics.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using DataGateMonitor.Tests.Helpers;
using Moq;

namespace DataGateMonitor.Tests.Services.Api;

public class VpnServerPiHoleConfigServiceHttpTests
{
    [Fact]
    public async Task GetMicroserviceDiagnosticsAsync_EnrichesWithDbSummaryAndHealth()
    {
        await using var harness = await CreateHarnessAsync();
        var lastStored = DateTimeOffset.UtcNow.AddMinutes(-3);
        harness.DnsQuery
            .Setup(x => x.GetServerSummaryAsync(harness.ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((42, lastStored));

        harness.Handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
                {
                  "success": true,
                  "data": {
                    "enabled": true,
                    "authenticated": true,
                    "baseUrl": "http://pi-hole:8080",
                    "collectorRunning": true,
                    "pollIntervalSeconds": 60,
                    "lastSuccessfulPollAtUtc": "{{DateTime.UtcNow.AddSeconds(-30):O}}",
                    "lastPollQueriesForwarded": 3,
                    "sampleQueryCount": 2
                  }
                }
                """)
        });

        var result = await harness.Sut.GetMicroserviceDiagnosticsAsync(harness.ServerId, CancellationToken.None);

        Assert.Equal(42, result.StoredQueryCount);
        Assert.Equal(lastStored.UtcDateTime, result.LastStoredQueryAtUtc);
        Assert.Equal("Ok", result.Health);
        Assert.Equal("http://pi-hole:8080", result.BaseUrl);
        Assert.Single(harness.Handler.Requests);
        Assert.Contains("api/pi-hole/diagnostics", harness.Handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.NotNull(harness.Handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task GetMicroserviceDiagnosticsAsync_Throws_WhenMicroserviceReturnsError()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("""{"success":false,"message":"upstream failed"}""")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Sut.GetMicroserviceDiagnosticsAsync(harness.ServerId, CancellationToken.None));

        Assert.Contains("502", ex.Message);
        Assert.Contains("upstream failed", ex.Message);
    }

    [Fact]
    public async Task ApplyRuntimeToMicroserviceAsync_PutsConfigToMicroservice()
    {
        await using var harness = await CreateHarnessAsync();
        await harness.Sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://pi-hole:8080",
            AppPassword = PiHoleTestFixtures.AppCredential,
            PollIntervalSeconds = 45,
            BatchSize = 100,
            LookbackSeconds = 90,
            ClientSubnetPrefix = "10.51.30."
        }, CancellationToken.None);

        harness.Handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Contains("api/pi-hole/config", req.RequestUri!.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true,"data":{}}""")
            };
        });

        await harness.Sut.ApplyRuntimeToMicroserviceAsync(harness.ServerId, CancellationToken.None);

        Assert.Single(harness.Handler.Requests);
        var row = await harness.Context.VpnServerPiHoleConfigs
            .AsNoTracking()
            .FirstAsync(x => x.VpnServerId == harness.ServerId);
        Assert.NotNull(row.LastRuntimeAppliedAtUtc);
    }

    [Fact]
    public async Task ApplyRuntimeToMicroserviceAsync_Throws_WhenApiUrlMissing()
    {
        await using var harness = await CreateHarnessAsync(apiUrl: "  ");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Sut.ApplyRuntimeToMicroserviceAsync(harness.ServerId, CancellationToken.None));
        Assert.Contains("API URL", ex.Message);
    }

    [Fact]
    public async Task GetMicroserviceDiagnosticsAsync_ForXray_UsesXrayAudience()
    {
        string? capturedAudience = null;
        await using var harness = await CreateHarnessAsync(
            serverType: DataGateMonitor.SharedModels.Enums.VpnServerType.Xray);
        harness.TokenService
            .Setup(x => x.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string _, string __, string ___, string audience) =>
            {
                capturedAudience = audience;
                return "test-token";
            });

        harness.Handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "success": true,
                  "data": {
                    "enabled": true,
                    "authenticated": true,
                    "baseUrl": "http://172.17.0.1:8080",
                    "collectorRunning": true
                  }
                }
                """)
        });

        await harness.Sut.GetMicroserviceDiagnosticsAsync(harness.ServerId, CancellationToken.None);

        Assert.Equal("DataGateXRayManager", capturedAudience);
        Assert.Contains("api/pi-hole/diagnostics", harness.Handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ApplyRuntimeToMicroserviceAsync_ForXray_UsesXrayAudience()
    {
        string? capturedAudience = null;
        await using var harness = await CreateHarnessAsync(
            serverType: DataGateMonitor.SharedModels.Enums.VpnServerType.Xray);
        harness.TokenService
            .Setup(x => x.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string _, string __, string ___, string audience) =>
            {
                capturedAudience = audience;
                return "test-token";
            });

        await harness.Sut.UpsertAsync(new UpsertVpnServerPiHoleConfigRequest
        {
            VpnServerId = harness.ServerId,
            BaseUrl = "http://172.17.0.1:8080",
            AppPassword = PiHoleTestFixtures.AppCredential,
            PollIntervalSeconds = 60,
            BatchSize = 200,
            LookbackSeconds = 120,
            ClientSubnetPrefix = ""
        }, CancellationToken.None);

        harness.Handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"data":{}}""")
        });

        await harness.Sut.ApplyRuntimeToMicroserviceAsync(harness.ServerId, CancellationToken.None);

        Assert.Equal("DataGateXRayManager", capturedAudience);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public IList<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory) => _responses.Enqueue(factory);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No queued HTTP response.");

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private static async Task<Harness> CreateHarnessAsync(
        string apiUrl = "http://microservice.test",
        DataGateMonitor.SharedModels.Enums.VpnServerType serverType = DataGateMonitor.SharedModels.Enums.VpnServerType.OpenVpn)
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
            ServerName = "pi-hole-http-test",
            ApiUrl = apiUrl,
            IsPiHoleEnabled = true,
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
        var handler = new QueueHandler();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        var tokenService = new Mock<IMicroserviceTokenService>();
        tokenService.Setup(x => x.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("test-token");

        var sut = new VpnServerPiHoleConfigService(
            vpnServerQuery.Object,
            piHoleConfigQuery,
            dnsQuery.Object,
            piHoleQuery,
            piHoleCommand,
            httpClientFactory.Object,
            tokenService.Object,
            NullLogger<VpnServerPiHoleConfigService>.Instance);

        return new Harness(connection, context, server.Id, sut, dnsQuery, handler, tokenService);
    }

    private sealed class Harness(
        SqliteConnection connection,
        ApplicationDbContext context,
        int serverId,
        VpnServerPiHoleConfigService sut,
        Mock<IVpnDnsQueryLogQueryService> dnsQuery,
        QueueHandler handler,
        Mock<IMicroserviceTokenService> tokenService) : IAsyncDisposable
    {
        public int ServerId { get; } = serverId;
        public VpnServerPiHoleConfigService Sut { get; } = sut;
        public ApplicationDbContext Context => context;
        public Mock<IVpnDnsQueryLogQueryService> DnsQuery { get; } = dnsQuery;
        public QueueHandler Handler { get; } = handler;
        public Mock<IMicroserviceTokenService> TokenService { get; } = tokenService;

        public async ValueTask DisposeAsync()
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
