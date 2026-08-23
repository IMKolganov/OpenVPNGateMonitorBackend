using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.VpnEvent.Requests;

namespace DataGateMonitor.Services.DataGateOpenVpnManager.Events;

/// <summary>
/// Final session accounting on OpenVPN client-disconnect: poll CLIENT_LIST may lag;
/// the disconnect hook carries authoritative bytes_received / bytes_sent for the whole session.
/// </summary>
internal static class VpnClientDisconnectFinalizer
{
    public static DateTimeOffset ResolveDisconnectedAt(VpnEventRequest req, DateTimeOffset nowUtc)
    {
        if (req.DisconnectedAt is { } at)
            return at.ToUniversalTime();
        if (req.ConnectedSince is { } since && req.DurationSec is { } dur)
            return since.ToUniversalTime().AddSeconds(dur);
        return nowUtc;
    }

    /// <summary>
    /// Mutates a tracked/client snapshot with final disconnect fields.
    /// Bytes are updated only when the event provides them (avoids wiping on empty payloads).
    /// </summary>
    public static void ApplyToClient(VpnServerClient client, VpnEventRequest req, DateTimeOffset nowUtc)
    {
        client.IsConnected = false;
        client.DisconnectedAt = ResolveDisconnectedAt(req, nowUtc);
        client.LastUpdate = nowUtc;
        if (req.BytesReceived is { } br)
            client.BytesReceived = br;
        if (req.BytesSent is { } bs)
            client.BytesSent = bs;
    }

    public static long SumBytesReceived(IEnumerable<VpnServerClient> clients) =>
        clients.Sum(c => c.BytesReceived);

    public static long SumBytesSent(IEnumerable<VpnServerClient> clients) =>
        clients.Sum(c => c.BytesSent);
}
