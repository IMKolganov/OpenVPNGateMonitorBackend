using System.Net;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Dto;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;

namespace DataGateMonitor.Services.DataGateXRayManager.Events;

/// <summary>
/// Shared Pi-hole hosts OpenVPN + Xray collectors. Xray must not persist OpenVPN LAN queries
/// (null CN + private client IP) that leaked through an unscoped node filter.
/// </summary>
public static class XrayDnsQueryBatchGuard
{
    public static DnsQueryBatchRequest FilterForPersistence(DnsQueryBatchRequest batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var kept = batch.Queries
            .Where(q => !string.IsNullOrWhiteSpace(q.CommonName) || !IsPrivateOrLoopback(q.ClientIp))
            .ToList();

        return new DnsQueryBatchRequest
        {
            CollectedAtUtc = batch.CollectedAtUtc,
            Queries = kept
        };
    }

    public static bool IsPrivateOrLoopback(string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
            return true;

        var host = clientIp.Trim();
        var colon = host.LastIndexOf(':');
        if (colon > 0
            && host.IndexOf('.') >= 0
            && int.TryParse(host[(colon + 1)..], out var port)
            && port is >= 0 and <= 65535)
        {
            host = host[..colon];
        }

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // Unique-local fc00::/7 and link-local fe80::/10 — same leak class as RFC1918 for null-CN rows.
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc)
                return true;
            if ((bytes[0] & 0xfe) == 0xfe && (bytes[1] & 0xc0) == 0x80)
                return true;
            return false;
        }

        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254)
               || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }
}
