using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.VpnEvent.Requests;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager.Events;

public class VpnClientDisconnectFinalizerTests
{
    [Fact]
    public void ApplyToClient_WritesFinalBytes_OverLastPollSnapshot()
    {
        var client = new VpnServerClient
        {
            CommonName = "cn-a",
            IsConnected = true,
            BytesReceived = 1_000, // last poll lagged
            BytesSent = 200
        };
        var connectedSince = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var disconnectedAt = connectedSince.AddMinutes(5);
        var now = disconnectedAt.AddSeconds(1);

        VpnClientDisconnectFinalizer.ApplyToClient(client, new VpnEventRequest
        {
            CommonName = "cn-a",
            ConnectedSince = connectedSince,
            DisconnectedAt = disconnectedAt,
            BytesReceived = 50_000, // final from client-disconnect
            BytesSent = 40_000
        }, now);

        Assert.False(client.IsConnected);
        Assert.Equal(disconnectedAt, client.DisconnectedAt);
        Assert.Equal(50_000, client.BytesReceived);
        Assert.Equal(40_000, client.BytesSent);
        Assert.Equal(now, client.LastUpdate);
    }

    [Fact]
    public void ApplyToClient_WhenBytesMissing_DoesNotWipePollCounters()
    {
        var client = new VpnServerClient
        {
            IsConnected = true,
            BytesReceived = 123,
            BytesSent = 456
        };

        VpnClientDisconnectFinalizer.ApplyToClient(client, new VpnEventRequest
        {
            CommonName = "cn-a",
            BytesReceived = null,
            BytesSent = null
        }, DateTimeOffset.UtcNow);

        Assert.False(client.IsConnected);
        Assert.Equal(123, client.BytesReceived);
        Assert.Equal(456, client.BytesSent);
    }

    [Fact]
    public void ResolveDisconnectedAt_PrefersExplicitThenDurationThenNow()
    {
        var now = new DateTimeOffset(2026, 8, 14, 15, 0, 0, TimeSpan.Zero);
        var since = now.AddMinutes(-10);
        var explicitAt = now.AddMinutes(-1);

        Assert.Equal(explicitAt,
            VpnClientDisconnectFinalizer.ResolveDisconnectedAt(
                new VpnEventRequest { DisconnectedAt = explicitAt, ConnectedSince = since, DurationSec = 999 },
                now));

        Assert.Equal(since.AddSeconds(120),
            VpnClientDisconnectFinalizer.ResolveDisconnectedAt(
                new VpnEventRequest { ConnectedSince = since, DurationSec = 120 },
                now));

        Assert.Equal(now,
            VpnClientDisconnectFinalizer.ResolveDisconnectedAt(new VpnEventRequest(), now));
    }

    [Fact]
    public void LiveConnectedSum_ExcludesDisconnected_ButHistoricalIncludesFinals()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new VpnServerClient
        {
            CommonName = "a",
            IsConnected = true,
            BytesReceived = 10_000,
            BytesSent = 1_000
        };
        var b = new VpnServerClient
        {
            CommonName = "b",
            IsConnected = true,
            BytesReceived = 2_000, // poll lag
            BytesSent = 500
        };

        // Live dashboard while both connected (CLIENT_LIST / status log path).
        var liveBefore = OpenVpnSummaryStatService.FromClientList(new OpenVpnManagementStatusResult
        {
            Clients = [CloneForStatus(a), CloneForStatus(b)]
        });
        Assert.Equal(12_000, liveBefore.BytesIn);
        Assert.Equal(1_500, liveBefore.BytesOut);

        // b disconnects with authoritative finals higher than last poll.
        VpnClientDisconnectFinalizer.ApplyToClient(b, new VpnEventRequest
        {
            CommonName = "b",
            BytesReceived = 8_000,
            BytesSent = 3_000,
            DisconnectedAt = now
        }, now);

        Assert.False(b.IsConnected);
        Assert.Equal(8_000, b.BytesReceived);
        Assert.Equal(3_000, b.BytesSent);

        // Live sum after disconnect: only still-connected sessions (CLIENT_LIST).
        var liveAfter = OpenVpnSummaryStatService.FromClientList(new OpenVpnManagementStatusResult
        {
            Clients = [CloneForStatus(a)]
        });
        Assert.Equal(10_000, liveAfter.BytesIn);
        Assert.Equal(1_000, liveAfter.BytesOut);

        // Historical / session table: all rows including disconnect finals.
        var all = new[] { a, b };
        Assert.Equal(18_000, VpnClientDisconnectFinalizer.SumBytesReceived(all));
        Assert.Equal(4_000, VpnClientDisconnectFinalizer.SumBytesSent(all));
        Assert.Equal(10_000 + 8_000, a.BytesReceived + b.BytesReceived);
    }

    private static VpnServerClient CloneForStatus(VpnServerClient c) => new()
    {
        CommonName = c.CommonName,
        BytesReceived = c.BytesReceived,
        BytesSent = c.BytesSent,
        IsConnected = c.IsConnected
    };
}
