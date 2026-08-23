using System.Text.RegularExpressions;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Api;

/// <summary>
/// Android ≤1.0.4 calls <c>GET /api/open-vpn-servers/get-all-with-status</c> and treats every WSS
/// node as TCP. UDP-WSS (<c>proto udp</c> in the ovpn template) and Xray in that list cause a
/// reconnect storm on udp nodes. Tags and display names are ignored.
/// </summary>
internal static partial class LegacyOpenVpnV1ServerFilter
{
    [GeneratedRegex(
        @"^\s*proto\s+(udp|udp4|udp6)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex UdpProtoDirective();

    public static bool IsVisibleToLegacyTcpOpenVpnClient(VpnServerType serverType, string? ovpnConfigTemplate)
    {
        if (serverType != VpnServerType.OpenVpn)
            return false;

        return !HasUdpProto(ovpnConfigTemplate);
    }

    internal static bool HasUdpProto(string? ovpnConfigTemplate) =>
        !string.IsNullOrWhiteSpace(ovpnConfigTemplate) && UdpProtoDirective().IsMatch(ovpnConfigTemplate);
}
