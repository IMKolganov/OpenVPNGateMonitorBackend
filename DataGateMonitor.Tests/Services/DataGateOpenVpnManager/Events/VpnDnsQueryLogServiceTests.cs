using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;
using DataGateMonitor.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager.Events;

public class VpnDnsQueryLogServiceTests
{
    [Fact]
    public async Task SaveBatchAsync_SkipsDuplicatesAndResolvesExternalId()
    {
        var existing = new VpnDnsQueryLog
        {
            Id = 1,
            VpnServerId = 1,
            PiHoleQueryId = 100,
            ClientIp = "10.51.30.1",
            Domain = "old.example",
            Status = "FORWARDED",
            QueriedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        await using var context = CreateContext();
        context.VpnDnsQueryLogs.Add(existing);
        await context.SaveChangesAsync();

        var query = CreateQueryService(context);
        var persisted = new List<VpnDnsQueryLog>();
        var command = new Mock<ICommandService<VpnDnsQueryLog, int>>();
        command.Setup(x => x.AddRange(It.IsAny<IEnumerable<VpnDnsQueryLog>>(), true, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<VpnDnsQueryLog>, bool, CancellationToken>((rows, _, _) => persisted.AddRange(rows))
            .ReturnsAsync(1);

        var issued = new Mock<IIssuedOvpnFileQueryService>();
        issued.Setup(x => x.GetExternalIdByCommonName("cn-new", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-42");
        var issuedXray = new Mock<IIssuedXrayClientLinkQueryService>();

        var sut = new VpnDnsQueryLogService(
            command.Object, query, issued.Object, issuedXray.Object, NullLogger<VpnDnsQueryLogService>.Instance);
        var batch = new DnsQueryBatchRequest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Queries = new List<DnsQueryEventDto>
            {
                new()
                {
                    PiHoleQueryId = 100,
                    ClientIp = "10.51.30.1",
                    Domain = "dup.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                },
                new()
                {
                    PiHoleQueryId = 101,
                    ClientIp = "10.51.30.2",
                    CommonName = "cn-new",
                    Domain = "new.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            }
        };

        var saved = await sut.SaveBatchAsync(1, batch, CancellationToken.None);

        Assert.Equal(1, saved);
        Assert.Single(persisted);
        Assert.Equal("ext-42", persisted[0].ExternalId);
        Assert.Equal(101, persisted[0].PiHoleQueryId);
    }

    [Fact]
    public async Task SaveBatchAsync_ResolvesExternalIdFromXrayLink_WhenOvpnMisses()
    {
        await using var context = CreateContext();
        var query = CreateQueryService(context);
        var persisted = new List<VpnDnsQueryLog>();
        var command = new Mock<ICommandService<VpnDnsQueryLog, int>>();
        command.Setup(x => x.AddRange(It.IsAny<IEnumerable<VpnDnsQueryLog>>(), true, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<VpnDnsQueryLog>, bool, CancellationToken>((rows, _, _) => persisted.AddRange(rows))
            .ReturnsAsync(1);

        var issued = new Mock<IIssuedOvpnFileQueryService>();
        issued.Setup(x => x.GetExternalIdByCommonName("vless-user", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var issuedXray = new Mock<IIssuedXrayClientLinkQueryService>();
        issuedXray.Setup(x => x.GetExternalIdByCommonName("vless-user", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ext-xray-9");

        var sut = new VpnDnsQueryLogService(
            command.Object, query, issued.Object, issuedXray.Object, NullLogger<VpnDnsQueryLogService>.Instance);

        var saved = await sut.SaveBatchAsync(7, new DnsQueryBatchRequest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 501,
                    ClientIp = "10.80.0.2",
                    CommonName = "vless-user",
                    Domain = "xray.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(1, saved);
        Assert.Single(persisted);
        Assert.Equal("ext-xray-9", persisted[0].ExternalId);
    }

    [Fact]
    public async Task SaveBatchAsync_AllowsNullCommonName_ForXrayIpOnlyRows()
    {
        await using var context = CreateContext();
        var query = CreateQueryService(context);
        var persisted = new List<VpnDnsQueryLog>();
        var command = new Mock<ICommandService<VpnDnsQueryLog, int>>();
        command.Setup(x => x.AddRange(It.IsAny<IEnumerable<VpnDnsQueryLog>>(), true, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<VpnDnsQueryLog>, bool, CancellationToken>((rows, _, _) => persisted.AddRange(rows))
            .ReturnsAsync(1);

        var sut = new VpnDnsQueryLogService(
            command.Object,
            query,
            new Mock<IIssuedOvpnFileQueryService>().Object,
            new Mock<IIssuedXrayClientLinkQueryService>().Object,
            NullLogger<VpnDnsQueryLogService>.Instance);

        var saved = await sut.SaveBatchAsync(7, new DnsQueryBatchRequest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 777,
                    ClientIp = "10.80.0.9",
                    CommonName = null,
                    Domain = "ip-only.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(1, saved);
        Assert.Single(persisted);
        Assert.Null(persisted[0].CommonName);
        Assert.Equal("10.80.0.9", persisted[0].ClientIp);
        Assert.Null(persisted[0].ExternalId);
    }

    [Fact]
    public async Task SaveBatchAsync_ReturnsZero_WhenAllDuplicates()
    {
        var existing = new VpnDnsQueryLog
        {
            Id = 1,
            VpnServerId = 1,
            PiHoleQueryId = 200,
            ClientIp = "10.51.30.1",
            Domain = "dup.example",
            Status = "FORWARDED",
            QueriedAtUtc = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        await using var context = CreateContext();
        context.VpnDnsQueryLogs.Add(existing);
        await context.SaveChangesAsync();

        var query = CreateQueryService(context);
        var command = new Mock<ICommandService<VpnDnsQueryLog, int>>();
        var issued = new Mock<IIssuedOvpnFileQueryService>();
        var issuedXray = new Mock<IIssuedXrayClientLinkQueryService>();
        var sut = new VpnDnsQueryLogService(
            command.Object, query, issued.Object, issuedXray.Object, NullLogger<VpnDnsQueryLogService>.Instance);

        var saved = await sut.SaveBatchAsync(1, new DnsQueryBatchRequest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 200,
                    ClientIp = "10.51.30.1",
                    Domain = "dup.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(0, saved);
        command.Verify(x => x.AddRange(It.IsAny<IEnumerable<VpnDnsQueryLog>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IQueryService<VpnDnsQueryLog, int> CreateQueryService(ApplicationDbContext context)
    {
        var queries = new Dictionary<Type, object>
        {
            [typeof(VpnDnsQueryLog)] = new TestQuery<VpnDnsQueryLog>(context.VpnDnsQueryLogs)
        };
        return new EfQueryService<VpnDnsQueryLog, int>(new TestUnitOfWork(queries));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataBaseSettings:DefaultSchema"] = "test_schema" })
            .Build();
        return new ApplicationDbContext(options, configuration);
    }
}
