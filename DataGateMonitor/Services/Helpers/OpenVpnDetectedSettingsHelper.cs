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

    public sealed class DetectedClientSettings
    {
        public int? Port { get; init; }
        public string? Proto { get; init; }
        public string? Cipher { get; init; }
        public string? DataCiphers { get; init; }
        public string? Auth { get; init; }
        public string? TlsVersionMin { get; init; }
        public string? ClientVerb { get; init; }

        public bool HasAny =>
            Port.HasValue
            || !string.IsNullOrWhiteSpace(Proto)
            || !string.IsNullOrWhiteSpace(Cipher)
            || !string.IsNullOrWhiteSpace(DataCiphers)
            || !string.IsNullOrWhiteSpace(Auth)
            || !string.IsNullOrWhiteSpace(TlsVersionMin)
            || !string.IsNullOrWhiteSpace(ClientVerb);
    }

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

            if (!TryExtractDetectedSettings(diagnostics.OpenVpn, out var detected) || !detected.HasAny)
                return;

            if (detected.Port is > 0 and <= 65535)
                config.VpnServerPort = detected.Port.Value;

            config.ConfigTemplate = ApplyClientDirectives(config.ConfigTemplate, detected);
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
        var ok = TryExtractDetectedSettings(openVpnInfo, out var detected);
        port = detected.Port;
        proto = detected.Proto;
        return ok && (port.HasValue || !string.IsNullOrWhiteSpace(proto));
    }

    public static bool TryExtractDetectedSettings(object openVpnInfo, out DetectedClientSettings settings)
    {
        int? port = null;
        string? proto = null;
        string? cipher = null;
        string? dataCiphers = null;
        string? auth = null;
        string? tlsVersionMin = null;
        string? clientVerb = null;

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

            proto = ReadString(cfg, "proto")?.Trim().ToLowerInvariant();
            if (proto is not ("tcp" or "udp"))
                proto = null;

            cipher = ReadString(cfg, "cipher");
            dataCiphers = ReadString(cfg, "dataCiphers") ?? ReadString(cfg, "data_ciphers");
            auth = ReadString(cfg, "auth");
            tlsVersionMin = ReadString(cfg, "tlsVersionMin") ?? ReadString(cfg, "tls_version_min");
            clientVerb = ReadString(cfg, "clientVerb") ?? ReadString(cfg, "client_verb");
        }

        settings = new DetectedClientSettings
        {
            Port = port,
            Proto = proto,
            Cipher = cipher,
            DataCiphers = dataCiphers,
            Auth = auth,
            TlsVersionMin = tlsVersionMin,
            ClientVerb = clientVerb
        };

        return settings.HasAny;
    }

    public static string ApplyClientDirectives(string template, DetectedClientSettings settings)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        var result = template;
        if (!string.IsNullOrWhiteSpace(settings.Proto))
            result = ReplaceOrInsertDirective(result, "proto", settings.Proto!);

        if (!string.IsNullOrWhiteSpace(settings.Cipher))
            result = ReplaceOrInsertDirective(result, "cipher", settings.Cipher!);

        if (!string.IsNullOrWhiteSpace(settings.DataCiphers))
            result = ReplaceOrInsertDirective(result, "data-ciphers", settings.DataCiphers!);
        else if (!string.IsNullOrWhiteSpace(settings.Cipher))
            result = RemoveDirective(result, "data-ciphers");

        if (!string.IsNullOrWhiteSpace(settings.Auth))
            result = ReplaceOrInsertDirective(result, "auth", settings.Auth!);

        if (!string.IsNullOrWhiteSpace(settings.TlsVersionMin))
            result = ReplaceOrInsertDirective(result, "tls-version-min", settings.TlsVersionMin!);

        if (!string.IsNullOrWhiteSpace(settings.ClientVerb))
            result = ReplaceOrInsertDirective(result, "verb", settings.ClientVerb!);

        return result;
    }

    public static string ReplaceProtoDirective(string template, string proto) =>
        ReplaceOrInsertDirective(template, "proto", proto);

    public static string ApplyCipherDirectives(string template, string? cipher, string? dataCiphers)
    {
        var result = template;
        if (!string.IsNullOrWhiteSpace(cipher))
            result = ReplaceOrInsertDirective(result, "cipher", cipher.Trim());
        if (!string.IsNullOrWhiteSpace(dataCiphers))
            result = ReplaceOrInsertDirective(result, "data-ciphers", dataCiphers.Trim());
        else if (!string.IsNullOrWhiteSpace(cipher))
            result = RemoveDirective(result, "data-ciphers");
        return result;
    }

    private static string? ReadString(JObject cfg, string name)
    {
        if (!TryGetPropertyIgnoreCase(cfg, name, out var token) || token is not { Type: JTokenType.String })
            return null;
        var value = token.Value<string>()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string RemoveDirective(string template, string name)
    {
        var pattern = $@"^\s*{Regex.Escape(name)}\s+\S+.*\r?\n?";
        return Regex.Replace(template, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
    }

    private static string ReplaceOrInsertDirective(string template, string name, string value)
    {
        var pattern = $@"^\s*{Regex.Escape(name)}\s+\S+.*$";
        if (Regex.IsMatch(template, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            return Regex.Replace(
                template,
                pattern,
                $"{name} {value}",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

        if (string.Equals(name, "data-ciphers", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(template, @"^\s*cipher\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline))
        {
            return Regex.Replace(
                template,
                @"(^\s*cipher\s+\S+.*$)",
                $"$1\n{name} {value}",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        if (string.Equals(name, "tls-version-min", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(template, @"^\s*remote-cert-tls\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline))
        {
            return Regex.Replace(
                template,
                @"(^\s*remote-cert-tls\s+\S+.*$)",
                $"$1\n{name} {value}",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        return $"{name} {value}\n{template}";
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
