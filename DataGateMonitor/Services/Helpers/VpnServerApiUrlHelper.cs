using System.Net;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Helpers;

internal static class VpnServerApiUrlHelper
{
    /// <summary>
    /// Canonical form for comparing or storing VPN manager ApiUrl values
    /// (scheme, host, port, path; trailing slash; no query/fragment).
    /// </summary>
    public static string NormalizeApiUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var trimmed = raw.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            if (!trimmed.Contains("://", StringComparison.Ordinal)
                && Uri.TryCreate("http://" + trimmed.TrimEnd('/'), UriKind.Absolute, out uri))
            {
                // accepted host:port as http
            }
            else
            {
                return trimmed.TrimEnd('/') + "/";
            }
        }

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath.TrimEnd('/'),
            Query = string.Empty,
            Fragment = string.Empty
        };
        if (builder.Path == "/")
            builder.Path = string.Empty;

        return builder.Uri.ToString().TrimEnd('/') + "/";
    }

    public static bool ApiUrlsEquivalent(string? a, string? b) =>
        string.Equals(NormalizeApiUrl(a), NormalizeApiUrl(b), StringComparison.OrdinalIgnoreCase);

    public static string? TryParsePublicIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var direct = TryParseHostIp(trimmed);
        if (direct is not null)
            return direct;

        return TryParseHostIp(SanitizeExportEndpointHost(trimmed));
    }

    public static bool UsesSchemeDefaultPort(string? apiUrl)
    {
        var normalized = NormalizeApiUrl(apiUrl);
        if (string.IsNullOrWhiteSpace(normalized)
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsDefaultPort;
    }

    /// <summary>Host + port extracted from a normalized manager ApiUrl.</summary>
    public static bool TryGetEndpointHostPort(string? apiUrl, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var normalized = NormalizeApiUrl(apiUrl);
        if (string.IsNullOrWhiteSpace(normalized)
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        port = uri.IsDefaultPort
            ? uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        return port is > 0 and <= 65535;
    }

    public static string? TryParseHostIp(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        if (!IPAddress.TryParse(host.Trim(), out var parsed))
            return null;

        if (parsed.IsIPv4MappedToIPv6)
            parsed = parsed.MapToIPv4();

        return parsed.ToString();
    }

    public readonly record struct ManagerEndpointKey(string Ip, int Port, VpnServerType ServerType);

    public static ManagerEndpointKey? BuildEndpointKey(string? apiUrl, VpnServerType serverType, string? resolvedHostIp)
    {
        if (string.IsNullOrWhiteSpace(resolvedHostIp)
            || !TryGetEndpointHostPort(apiUrl, out _, out var port))
        {
            return null;
        }

        return new ManagerEndpointKey(resolvedHostIp, port, serverType);
    }

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
