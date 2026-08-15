using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataGateMonitor.Tests.Services.DataGateXRayManager.Events;

public class XrayDnsEventClientHandleTests
{
    [Fact]
    public async Task HandleDnsQueriesAsync_SavesBatchViaDnsLogService()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        dns.Setup(d => d.SaveBatchAsync(42, It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var sut = CreateSut(dns.Object, serverId: 42);
        var batch = new DnsQueryBatchRequest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 1,
                    ClientIp = "10.80.0.3",
                    CommonName = null,
                    Domain = "a.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                },
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 2,
                    ClientIp = "10.80.0.4",
                    Domain = "b.example",
                    Status = "GRAVITY",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };

        await sut.HandleDnsQueriesAsync(batch);

        dns.Verify(d => d.SaveBatchAsync(
            42,
            It.Is<DnsQueryBatchRequest>(b => b.Queries.Count == 2 && b.Queries[0].CommonName == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDnsQueriesAsync_EmptyBatch_DoesNotCallSave()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        var sut = CreateSut(dns.Object);

        await sut.HandleDnsQueriesAsync(new DnsQueryBatchRequest { Queries = [] });

        dns.Verify(d => d.SaveBatchAsync(It.IsAny<int>(), It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleDnsQueriesAsync_WhenSaveThrows_DoesNotPropagate()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        dns.Setup(d => d.SaveBatchAsync(It.IsAny<int>(), It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var sut = CreateSut(dns.Object);
        var batch = new DnsQueryBatchRequest
        {
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 9,
                    ClientIp = "10.80.0.1",
                    Domain = "x.example",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };

        var ex = await Record.ExceptionAsync(() => sut.HandleDnsQueriesAsync(batch));
        Assert.Null(ex);
    }

    private static XrayDnsEventClient CreateSut(IVpnDnsQueryLogService dns, int serverId = 1)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dns);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new XrayDnsEventClient(
            new VpnServer
            {
                Id = serverId,
                ServerType = VpnServerType.Xray,
                ServerName = "x",
                ApiUrl = "https://xray.example/",
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow
            },
            NullLogger<XrayDnsEventClient>.Instance,
            Mock.Of<IMicroserviceTokenService>(),
            scopeFactory);
    }
}
