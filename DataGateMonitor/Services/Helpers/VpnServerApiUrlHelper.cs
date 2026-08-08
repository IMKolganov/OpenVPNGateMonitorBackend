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
        if (IsUsableEndpoint(configVpnServerIp))
            return Truncate(configVpnServerIp!.Trim());
        return Truncate(TryHostFromApiUrl(apiUrl) ?? string.Empty);
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

        // Reject loopback / unspecified placeholders often stored when IP detection fails.
        if (IPAddress.TryParse(v, out var address) &&
            (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            return false;

        return true;
    }

    private static string Truncate(string value) =>
        value.Length > 255 ? value[..255] : value;
}
