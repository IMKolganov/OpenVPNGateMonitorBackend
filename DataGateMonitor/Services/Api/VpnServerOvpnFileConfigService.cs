using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Serialization;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace DataGateMonitor.Services.Api;

public class VpnServerOvpnFileConfigService(
    ILogger<VpnServerOvpnFileConfigService> logger, 
    IVpnServerOvpnFileConfigQueryService  openVpnServerOvpnFileConfigQueryService,
    ICommandService<VpnServerOvpnFileConfig, int> openVpnServerOvpnFileConfigCommandService,
    IMicroserviceInfoService microserviceInfoService)
    : IVpnServerOvpnFileConfigService
{
    private static readonly TimeSpan OpenVpnAutoDetectTimeout = TimeSpan.FromSeconds(5);
    private readonly ILogger<VpnServerOvpnFileConfigService> _logger = logger;


    public async Task<VpnServerOvpnFileConfig> GetVpnServerOvpnFileConfigByServerId(int vpnServerId, 
        CancellationToken ct)
    {
        return await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(vpnServerId, ct)
               ?? throw new InvalidOperationException("OvpnFileConfig not found");
    }
    
    public async Task<VpnServerOvpnFileConfig> AddOrUpdateVpnServerOvpnFileConfigByServerId(
        VpnServerOvpnFileConfig openVpnServerOvpnFileConfig, bool autoDetectServerSettings, CancellationToken ct)
    {
        if (autoDetectServerSettings)
            await TryApplyDetectedOpenVpnSettingsAsync(openVpnServerOvpnFileConfig, ct);

        var existingConfig = await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
            openVpnServerOvpnFileConfig.VpnServerId, ct);

        if (existingConfig != null)
        {
            existingConfig.VpnServerIp = openVpnServerOvpnFileConfig.VpnServerIp;
            existingConfig.VpnServerPort = openVpnServerOvpnFileConfig.VpnServerPort;
            existingConfig.ConfigTemplate = openVpnServerOvpnFileConfig.ConfigTemplate;
            existingConfig.LastUpdate = DateTimeOffset.UtcNow;

            await openVpnServerOvpnFileConfigCommandService.Update(existingConfig, true, ct);
        }
        else
        {
            openVpnServerOvpnFileConfig.CreateDate = DateTimeOffset.UtcNow;
            openVpnServerOvpnFileConfig.LastUpdate = DateTimeOffset.UtcNow;
            
            await openVpnServerOvpnFileConfigCommandService.Add(openVpnServerOvpnFileConfig, true, ct);
        }
        
        return await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
                   openVpnServerOvpnFileConfig.VpnServerId, ct)
               ?? throw new InvalidOperationException($"OpenVPN server OVPN file configuration not found for " +
                                                      $"server ID {openVpnServerOvpnFileConfig.VpnServerId}.");
    }

    private async Task TryApplyDetectedOpenVpnSettingsAsync(VpnServerOvpnFileConfig config, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(OpenVpnAutoDetectTimeout);
            var diagnostics = await microserviceInfoService.GetInfoAsync(config.VpnServerId, timeoutCts.Token);
            if (diagnostics is null || diagnostics.ServerType != VpnServerType.OpenVpn || diagnostics.OpenVpn is null)
                return;

            if (!TryExtractPortProto(diagnostics.OpenVpn, out var port, out var proto))
                return;

            if (port is > 0 and <= 65535)
                config.VpnServerPort = port.Value;

            if (!string.IsNullOrWhiteSpace(proto))
                config.ConfigTemplate = ReplaceProtoDirective(config.ConfigTemplate, proto);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Failed to auto-detect OpenVPN port/proto for VpnServerId={VpnServerId}. Keeping provided values.",
                config.VpnServerId);
        }
    }

    private static bool TryExtractPortProto(object openVpnInfo, out int? port, out string? proto)
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
                         int.TryParse(portToken.Value<string>(), out var parsedPort))
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

    private static string ReplaceProtoDirective(string template, string proto)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        if (Regex.IsMatch(template, @"^\s*proto\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            return Regex.Replace(template, @"^\s*proto\s+\S+", $"proto {proto}", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return $"proto {proto}\n{template}";
    }
}
