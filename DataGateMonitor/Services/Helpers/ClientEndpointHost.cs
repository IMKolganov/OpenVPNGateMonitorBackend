using System.Net;

namespace DataGateMonitor.Services.Helpers;

/// <summary>Parses host/IP from a client endpoint string for GeoIP lookup.</summary>
public static class ClientEndpointHost
{
    /// <summary>
    /// Supports <c>ipv4:port</c>, <c>[ipv6]:port</c>, plain IPv4/hostname, or IPv6 without port.
    /// </summary>
    public static string? TryGetHostForGeoLookup(string? remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(remoteAddress))
            return null;

        var s = remoteAddress.Trim();

        if (s.StartsWith('['))
        {
            var end = s.IndexOf(']', StringComparison.Ordinal);
            if (end > 1)
                return s[1..end];
            return null;
        }

        var colonCount = s.Count(c => c == ':');
        if (colonCount == 1)
        {
            var idx = s.IndexOf(':');
            var host = s[..idx];
            if (host.Length > 0 && !host.Contains(':'))
                return host;
        }

        return s;
    }

    public static bool IsNonLoopbackIpOrHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (!IPAddress.TryParse(host, out var ip))
            return true;

        return !IPAddress.IsLoopback(ip);
    }

    /// <summary>
    /// True when the endpoint host is RFC1918 / loopback / IPv6 ULA or link-local
    /// (typical WSS/docker proxy peer toward Xray/OpenVPN).
    /// </summary>
    public static bool IsPrivateOrLoopbackEndpoint(string? remoteAddress)
    {
        var host = TryGetHostForGeoLookup(remoteAddress);
        if (host is null || !IPAddress.TryParse(host, out var ip))
            return false;

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || bytes[0] == 127
                   || (bytes[0] == 169 && bytes[1] == 254)
                   || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc)
                return true;
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                return true;
        }

        return false;
    }
}
