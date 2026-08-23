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
            .ReturnsAsync(1);

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
                    ClientIp = "203.0.113.9",
                    Domain = "b.example",
                    Status = "GRAVITY",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };

        await sut.HandleDnsQueriesAsync(batch);

        // Private + null CN dropped (OpenVPN leak); public IP kept.
        dns.Verify(d => d.SaveBatchAsync(
            42,
            It.Is<DnsQueryBatchRequest>(b => b.Queries.Count == 1 && b.Queries[0].ClientIp == "203.0.113.9"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDnsQueriesAsync_DropsPrivateNullCnOpenVpnLeak_DoesNotSave()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        var sut = CreateSut(dns.Object);

        await sut.HandleDnsQueriesAsync(new DnsQueryBatchRequest
        {
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 1,
                    ClientIp = "10.51.15.7",
                    CommonName = null,
                    Domain = "wildberries.ru",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        });

        dns.Verify(d => d.SaveBatchAsync(It.IsAny<int>(), It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleDnsQueriesAsync_FiveXrayServers_EachPersistsOnlyOwnServerId()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        var savedByServer = new Dictionary<int, List<string>>();

        dns.Setup(d => d.SaveBatchAsync(It.IsAny<int>(), It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, DnsQueryBatchRequest, CancellationToken>((serverId, batch, _) =>
            {
                savedByServer[serverId] = batch.Queries.Select(q => q.Domain!).ToList();
            })
            .ReturnsAsync((int _, DnsQueryBatchRequest batch, CancellationToken _) => batch.Queries.Count);

        // Shared leak batch (OpenVPN LAN) + one public row per xray server identity in domain.
        var xrayServerIds = new[] { 71, 72, 73, 74, 75 };
        foreach (var serverId in xrayServerIds)
        {
            var sut = CreateSut(dns.Object, serverId);
            await sut.HandleDnsQueriesAsync(new DnsQueryBatchRequest
            {
                CollectedAtUtc = DateTimeOffset.UtcNow,
                Queries =
                [
                    new DnsQueryEventDto
                    {
                        PiHoleQueryId = serverId * 10,
                        ClientIp = "10.51.15.7",
                        CommonName = null,
                        Domain = "stolen-from-ovpn.example",
                        Status = "FORWARDED",
                        QueriedAtUtc = DateTimeOffset.UtcNow
                    },
                    new DnsQueryEventDto
                    {
                        PiHoleQueryId = serverId * 10 + 1,
                        ClientIp = $"203.0.113.{serverId}",
                        CommonName = null,
                        Domain = $"xray-{serverId}.example",
                        Status = "FORWARDED",
                        QueriedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            });
        }

        Assert.Equal(5, savedByServer.Count);
        foreach (var serverId in xrayServerIds)
        {
            Assert.True(savedByServer.ContainsKey(serverId));
            Assert.Single(savedByServer[serverId]);
            Assert.Equal($"xray-{serverId}.example", savedByServer[serverId][0]);
            Assert.DoesNotContain("stolen-from-ovpn.example", savedByServer[serverId]);
        }

        dns.Verify(
            d => d.SaveBatchAsync(It.IsAny<int>(), It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task HandleDnsQueriesAsync_KeepsPrivateIp_WhenCommonNameMatchesUser()
    {
        var dns = new Mock<IVpnDnsQueryLogService>(MockBehavior.Strict);
        dns.Setup(d => d.SaveBatchAsync(76, It.IsAny<DnsQueryBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = CreateSut(dns.Object, serverId: 76);
        await sut.HandleDnsQueriesAsync(new DnsQueryBatchRequest
        {
            Queries =
            [
                new DnsQueryEventDto
                {
                    PiHoleQueryId = 100,
                    ClientIp = "10.80.1.5",
                    CommonName = "adg-76-105824625148468116460-X1wJLVTFSB6ZwasN0hyxgA",
                    Domain = "openai.com",
                    Status = "FORWARDED",
                    QueriedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        });

        dns.Verify(d => d.SaveBatchAsync(
            76,
            It.Is<DnsQueryBatchRequest>(b =>
                b.Queries.Count == 1
                && b.Queries[0].CommonName!.StartsWith("adg-76-", StringComparison.Ordinal)
                && b.Queries[0].ClientIp == "10.80.1.5"),
            It.IsAny<CancellationToken>()), Times.Once);
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
                    ClientIp = "203.0.113.1",
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
