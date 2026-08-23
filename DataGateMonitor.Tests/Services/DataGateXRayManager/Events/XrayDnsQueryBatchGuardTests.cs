using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;

namespace DataGateMonitor.Tests.Services.DataGateXRayManager.Events;

public class XrayDnsQueryBatchGuardTests
{
    [Theory]
    [InlineData("10.51.15.7", true)]
    [InlineData("10.51.16.2", true)]
    [InlineData("10.80.0.3", true)]
    [InlineData("172.20.0.2", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("203.0.113.9", false)]
    [InlineData("164.215.15.224", false)]
    [InlineData("203.0.113.9:443", false)]
    [InlineData("fc00::1", true)]
    [InlineData("fd12:3456:789a::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("2001:db8::1", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void IsPrivateOrLoopback_ClassifiesClientIps(string? ip, bool expected) =>
        Assert.Equal(expected, XrayDnsQueryBatchGuard.IsPrivateOrLoopback(ip));

    [Fact]
    public void FilterForPersistence_DropsIpv6UlaWithoutCommonName()
    {
        var batch = Batch(Q(1, "fd12:3456:789a::10", cn: null, "leak.example"));
        Assert.Empty(XrayDnsQueryBatchGuard.FilterForPersistence(batch).Queries);
    }

    [Fact]
    public void FilterForPersistence_DropsOpenVpnLanRowsWithoutCommonName()
    {
        var batch = Batch(
            Q(1, "10.51.15.7", cn: null, "wildberries.ru"),
            Q(2, "10.51.16.4", cn: null, "yandex.ru"),
            Q(3, "10.50.28.9", cn: null, "ovpn-other.ru"));

        var filtered = XrayDnsQueryBatchGuard.FilterForPersistence(batch);

        Assert.Empty(filtered.Queries);
    }

    [Fact]
    public void FilterForPersistence_KeepsPrivateIp_WhenCommonNamePresent()
    {
        var batch = Batch(
            Q(1, "10.80.0.3", cn: "adg-76-user-a", "openai.com"));

        var filtered = XrayDnsQueryBatchGuard.FilterForPersistence(batch);

        Assert.Single(filtered.Queries);
        Assert.Equal("adg-76-user-a", filtered.Queries[0].CommonName);
        Assert.Equal("10.80.0.3", filtered.Queries[0].ClientIp);
    }

    [Fact]
    public void FilterForPersistence_KeepsPublicIp_WithoutCommonName()
    {
        var batch = Batch(
            Q(1, "203.0.113.50", cn: null, "example.com"));

        var filtered = XrayDnsQueryBatchGuard.FilterForPersistence(batch);

        Assert.Single(filtered.Queries);
        Assert.Equal("203.0.113.50", filtered.Queries[0].ClientIp);
    }

    [Fact]
    public void FilterForPersistence_SharedHost_FiveOpenVpnPlusFiveXray_OnlyKeepsAttributedOrPublic()
    {
        // One shared Pi-hole dump: 5 OpenVPN LAN prefixes + 5 Xray-ish rows (CN or public IP).
        var ovpnPrefixes = new[] { "10.51.11.", "10.51.12.", "10.51.13.", "10.51.14.", "10.51.15." };
        var queries = new List<DnsQueryEventDto>();
        var id = 1;
        foreach (var prefix in ovpnPrefixes)
        {
            queries.Add(Q(id++, $"{prefix}10", cn: null, $"ovpn-{prefix}leak.example"));
            // OpenVPN-enriched row that somehow landed in an Xray batch — keep (has CN).
            queries.Add(Q(id++, $"{prefix}20", cn: $"cn-ovpn-{prefix.TrimEnd('.')}", $"ovpn-{prefix}named.example"));
        }

        queries.Add(Q(id++, "10.80.1.2", cn: "adg-xray-1", "xray1.example"));
        queries.Add(Q(id++, "10.80.2.2", cn: "adg-xray-2", "xray2.example"));
        queries.Add(Q(id++, "10.80.3.2", cn: "adg-xray-3", "xray3.example"));
        queries.Add(Q(id++, "203.0.113.11", cn: null, "xray4-public.example"));
        queries.Add(Q(id++, "198.51.100.22", cn: "adg-xray-5", "xray5.example"));

        var filtered = XrayDnsQueryBatchGuard.FilterForPersistence(Batch(queries.ToArray()));

        // 5 ovpn null-CN leaks dropped; 5 ovpn-with-CN + 5 xray rows kept.
        Assert.Equal(10, filtered.Queries.Count);
        Assert.DoesNotContain(filtered.Queries, q => q.CommonName is null && q.ClientIp.StartsWith("10.51.", StringComparison.Ordinal));
        Assert.Contains(filtered.Queries, q => q.Domain == "xray4-public.example");
        Assert.Contains(filtered.Queries, q => q.CommonName == "adg-xray-1");
        Assert.Contains(filtered.Queries, q => q.CommonName == "cn-ovpn-10.51.15");
    }

    [Fact]
    public void FilterForPersistence_PreservesCollectedAtUtc()
    {
        var at = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var batch = new DnsQueryBatchRequest
        {
            CollectedAtUtc = at,
            Queries = [Q(1, "203.0.113.1", null, "a.example")]
        };

        var filtered = XrayDnsQueryBatchGuard.FilterForPersistence(batch);
        Assert.Equal(at, filtered.CollectedAtUtc);
    }

    private static DnsQueryBatchRequest Batch(params DnsQueryEventDto[] queries) => new()
    {
        CollectedAtUtc = DateTimeOffset.UtcNow,
        Queries = queries.ToList()
    };

    private static DnsQueryEventDto Q(long id, string ip, string? cn, string domain) => new()
    {
        PiHoleQueryId = id,
        ClientIp = ip,
        CommonName = cn,
        Domain = domain,
        Status = "FORWARDED",
        QueriedAtUtc = DateTimeOffset.UtcNow
    };
}
