using System.Globalization;
using System.Text.RegularExpressions;
using DataGateMonitor.Models;
using DataGateMonitor.Serialization;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using Newtonsoft.Json.Linq;

namespace DataGateMonitor.Services.Helpers;

internal static class OpenVpnDetectedSettingsHelper
{
    public static readonly TimeSpan DefaultAutoDetectTimeout = TimeSpan.FromSeconds(5);

    public static async Task TryApplyAsync(
        int vpnServerId,
        VpnServerOvpnFileConfig config,
        IMicroserviceInfoService microserviceInfoService,
        ILogger logger,
        string logMessage,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout ?? DefaultAutoDetectTimeout);
            var diagnostics = await microserviceInfoService.GetInfoAsync(vpnServerId, timeoutCts.Token);
            if (diagnostics is null || diagnostics.ServerType != VpnServerType.OpenVpn || diagnostics.OpenVpn is null)
                return;

            ApplyPublicIpIfEmpty(config, diagnostics.OpenVpn.PublicIp);

            if (!TryExtractPortProto(diagnostics.OpenVpn, out var port, out var proto))
                return;

            if (port is > 0 and <= 65535)
                config.VpnServerPort = port.Value;

            if (!string.IsNullOrWhiteSpace(proto))
                config.ConfigTemplate = ReplaceProtoDirective(config.ConfigTemplate, proto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, logMessage, vpnServerId);
        }
    }

    /// <summary>
    /// Fills <see cref="VpnServerOvpnFileConfig.VpnServerIp"/> from the node <c>PublicIp</c> only when
    /// the config IP is empty — never from the dashboard's outbound address.
    /// </summary>
    public static void ApplyPublicIpIfEmpty(VpnServerOvpnFileConfig config, string? publicIp)
    {
        if (!string.IsNullOrWhiteSpace(config.VpnServerIp))
            return;
        if (string.IsNullOrWhiteSpace(publicIp))
            return;
        config.VpnServerIp = publicIp.Trim();
    }

    public static bool TryExtractPortProto(object openVpnInfo, out int? port, out string? proto)
    {
        port = null;
        proto = null;

        var root = JObject.FromObject(openVpnInfo, Newtonsoft.Json.JsonSerializer.Create(ProjectJson.WebSettings));
        if (TryGetPropertyIgnoreCase(root, "config", out var cfgToken) && cfgToken is JObject cfg)
        {
            if (TryGetPropertyIgnoreCase(cfg, "port", out var portToken))
            {
                if (portToken is { Type: JTokenType.Integer })
                    port = portToken.Value<int>();
                else if (portToken is { Type: JTokenType.String } &&
                         int.TryParse(
                             portToken.Value<string>(),
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out var parsedPort))
                    port = parsedPort;
            }

            if (TryGetPropertyIgnoreCase(cfg, "proto", out var protoToken) && protoToken is { Type: JTokenType.String })
            {
                var p = protoToken.Value<string>()?.Trim().ToLowerInvariant();
                if (p is "tcp" or "udp")
                    proto = p;
            }
        }

        return port.HasValue || !string.IsNullOrWhiteSpace(proto);
    }

    public static string ReplaceProtoDirective(string template, string proto)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        if (Regex.IsMatch(template, @"^\s*proto\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            return Regex.Replace(template, @"^\s*proto\s+\S+", $"proto {proto}", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return $"proto {proto}\n{template}";
    }

    private static bool TryGetPropertyIgnoreCase(JObject obj, string propertyName, out JToken? value)
    {
        foreach (var prop in obj.Properties())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
