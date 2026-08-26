using DataGateMonitor.Serialization;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Helpers;

/// <summary>
/// Resolves node identity for discovery matching: public IP + manager API port + stack type.
/// Used when announce URL (direct IP:port) differs from dashboard ApiUrl (domain/nginx).
/// </summary>
internal static class VpnServerNodeIdentityResolver
{
    public static string? TryResolvePublicIp(string? vpnServerIp, string? announcePublicIp, string? apiUrlHostIp)
    {
        var fromConfig = VpnServerApiUrlHelper.TryParsePublicIp(vpnServerIp);
        if (fromConfig is not null)
            return fromConfig;

        var fromAnnounce = VpnServerApiUrlHelper.TryParsePublicIp(announcePublicIp);
        if (fromAnnounce is not null)
            return fromAnnounce;

        return VpnServerApiUrlHelper.TryParsePublicIp(apiUrlHostIp);
    }

    public static int? TryResolveManagerApiPort(string? apiUrl, VpnServerType serverType, string? conflogPayloadJson)
    {
        if (VpnServerApiUrlHelper.TryGetEndpointHostPort(apiUrl, out _, out var urlPort)
            && !VpnServerApiUrlHelper.UsesSchemeDefaultPort(apiUrl))
        {
            return urlPort;
        }

        return TryParseManagerApiPortFromConflog(conflogPayloadJson, serverType);
    }

    public static VpnServerApiUrlHelper.ManagerEndpointKey? BuildNodeIdentityKey(
        string? publicIp,
        int? managerApiPort,
        VpnServerType serverType)
    {
        var normalizedIp = VpnServerApiUrlHelper.TryParsePublicIp(publicIp);
        if (normalizedIp is null || managerApiPort is not (> 0 and <= 65535))
            return null;

        return new VpnServerApiUrlHelper.ManagerEndpointKey(normalizedIp, managerApiPort.Value, serverType);
    }

    public static int? TryParseManagerApiPortFromConflog(string? payloadJson, VpnServerType serverType)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            var dto = ProjectJson.Deserialize<VpnMicroserviceDiagnosticsDto>(payloadJson);
            if (dto is null)
                return null;

            var raw = serverType == VpnServerType.Xray
                ? dto.Xray?.Config?.ApiPort
                : dto.OpenVpn?.Config?.ApiPort;

            return int.TryParse(raw, out var port) && port is > 0 and <= 65535 ? port : null;
        }
        catch
        {
            return null;
        }
    }
}
