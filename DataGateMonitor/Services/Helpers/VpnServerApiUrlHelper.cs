using System.Net;

namespace DataGateMonitor.Services.Helpers;

internal static class VpnServerApiUrlHelper
{
    /// <summary>
    /// Reported "remote" for status UI: node PublicIp from /api/info, then Config IP,
    /// then host from ApiUrl. Never the dashboard WAN IP.
    /// </summary>
    public static string ResolveReportedRemoteIp(
        string? nodePublicIp,
        string? configVpnServerIp,
        string? apiUrl)
    {
        if (IsUsableEndpoint(nodePublicIp))
            return Truncate(nodePublicIp!.Trim());
        var configHost = SanitizeExportEndpointHost(configVpnServerIp);
        if (IsUsableEndpoint(configHost))
            return Truncate(configHost);
        return Truncate(TryHostFromApiUrl(apiUrl) ?? string.Empty);
    }

    /// <summary>Hostname for VLESS export — strips <c>https://</c>, paths, and inline <c>:port</c> mistakes.</summary>
    public static string SanitizeExportEndpointHost(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
            return string.Empty;

        v = v.TrimEnd('/');

        if (v.Contains("//", StringComparison.Ordinal)
            || v.StartsWith("http:", StringComparison.OrdinalIgnoreCase))
        {
            var host = TryHostFromApiUrl(v);
            if (!string.IsNullOrWhiteSpace(host))
                return Truncate(host);
        }

        if (!v.Contains('/') && v.Count(c => c == ':') == 1)
        {
            var idx = v.IndexOf(':');
            if (idx > 0 && int.TryParse(v.AsSpan(idx + 1), out var p) && p is > 0 and <= 65535)
                return Truncate(v[..idx]);
        }

        return Truncate(v);
    }

    public static string? TryHostFromApiUrl(string? apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            return null;
        if (!Uri.TryCreate(apiUrl.Trim(), UriKind.Absolute, out var uri))
            return null;
        return string.IsNullOrWhiteSpace(uri.Host) ? null : uri.Host;
    }

    private static bool IsUsableEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var v = value.Trim();
        if (v is "0.0.0.0" or "::" or "-" or "N/A")
            return false;
        if (v.Contains("//", StringComparison.Ordinal))
            return false;

        // Reject loopback / unspecified placeholders often stored when IP detection fails.
        if (IPAddress.TryParse(v, out var address) &&
            (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            return false;

        return true;
    }

    private static string Truncate(string value) =>
        value.Length > 255 ? value[..255] : value;
}
