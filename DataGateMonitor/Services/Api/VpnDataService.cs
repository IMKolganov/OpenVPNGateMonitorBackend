using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.Helpers.Interfaces;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Api.PostSetup;
using DataGateMonitor.Serialization;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace DataGateMonitor.Services.Api;

public class VpnDataService(
    ILogger<IVpnDataService> logger,
    IExternalIpAddressService externalIpAddressService,
    IQuotaPlanQueryService quotaPlanQueryService,
    IVpnServerQueryService openVpnServerQueryService,
    IVpnServerOvpnFileConfigQueryService openVpnServerOvpnFileConfigQueryService,
    ITransactionRunner transactionRunner,
    ICommandService<VpnServer, int> openVpnServerCommandService,
    ICommandService<VpnServerOvpnFileConfig, int> openVpnServerOvpnFileConfigCommandService,
    ICommandService<QuotaPlanAllowedServer, int> quotaPlanAllowedServerCommandService,
    ICommandService<VpnServerTag, int> openVpnServerTagCommandService,
    IServerOpenVpnNotificationService serverOpenVpnNotificationService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IMicroserviceInfoService microserviceInfoService,
    IOpenVpnMicroserviceClientFactory microserviceClientFactory,
    IOpenVpnEventClientFactory eventClientFactory) : IVpnDataService
{
    private static readonly TimeSpan ExternalIpResolveTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OpenVpnAutoDetectTimeout = TimeSpan.FromSeconds(5);

    public async Task<VpnServer> AddVpnServer(VpnServer server, List<int> quotaPlanIds, List<int> tagIds, CancellationToken ct)
    {
        var result = await transactionRunner.RunAsync(async _ =>
        {
            var now = DateTimeOffset.UtcNow;

            if (await openVpnServerQueryService.AnyByServerName(server.ServerName, ct))
            {
                logger.LogWarning("VPN server with name '{ServerName}' already exists", server.ServerName);
                throw new InvalidOperationException("A VPN server with the same name already exists.");
            }
            
            if (server.IsDefault)
            {
                // Unset the previous default in one SQL statement (no entity loading)
                await openVpnServerCommandService.UpdateWhere(
                    s => s.IsDefault,
                    u => u.SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.LastUpdate, now),
                    ct);
            }

            // Insert server (need ID immediately for further operations)
            server.CreateDate = now;
            server.LastUpdate = now;
            await openVpnServerCommandService.Add(server, saveChanges: true, ct);

            var effectiveQuotaPlanIds = quotaPlanIds.Count > 0
                ? quotaPlanIds
                : (await quotaPlanQueryService.GetDefault(ct)) is { } defaultPlan
                    ? [defaultPlan.Id]
                    : [];

            await SyncQuotaPlanLinksAsync(server.Id, effectiveQuotaPlanIds, ct);
            await SyncTagLinksAsync(server.Id, tagIds, ct);

            // Return a fresh snapshot
            return await openVpnServerQueryService.GetById(server.Id, ct)
                   ?? throw new InvalidOperationException("OpenVPN server not found");
        }, ct);

        await serverOpenVpnNotificationService.NotifyAdded(result.Id, result.ServerName, ct);
        statusCacheGenerationService.Bump();
        return result;
    }


    public async Task<VpnServer> UpdateVpnServer(VpnServer server, List<int> quotaPlanIds, List<int> tagIds, CancellationToken ct)
    {
        var result = await transactionRunner.RunAsync(async _ =>
        {
            var now = DateTimeOffset.UtcNow;

            if (await openVpnServerQueryService.AnyByServerNameExceptId(server.ServerName, server.Id, ct))
            {
                logger.LogWarning("VPN server with name '{ServerName}' already exists", server.ServerName);
                throw new InvalidOperationException("A VPN server with the same name already exists.");
            }

            if (server.IsDefault)
            {
                // Unset all other defaults in a single SQL statement
                await openVpnServerCommandService.UpdateWhere(
                    s => s.IsDefault && s.Id != server.Id,
                    u => u.SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.LastUpdate, now),
                    ct);
            }

            // Update this server
            server.LastUpdate = now;
            await openVpnServerCommandService.Update(server, saveChanges: true, ct);

            await SyncQuotaPlanLinksAsync(server.Id, quotaPlanIds, ct);
            await SyncTagLinksAsync(server.Id, tagIds, ct);

            // Backfill export config for servers created before post-setup ran (no-op when config already exists).
            await EnsureDefaultExportConfigIfMissingAsync(server, ct);

            // Return fresh snapshot
            return await openVpnServerQueryService.GetById(server.Id, ct)
                   ?? throw new InvalidOperationException("OpenVPN server not found");
        }, ct);

        await serverOpenVpnNotificationService.NotifyUpdated(result.Id, result.ServerName, ct);
        statusCacheGenerationService.Bump();
        microserviceClientFactory.Invalidate(result.Id);
        eventClientFactory.Remove(result.Id);
        return result;
    }


    public async Task<bool> DeleteVpnServer(int vpnServerId, CancellationToken ct)
    {
        var openVpnServer = await openVpnServerQueryService.GetById(vpnServerId, ct)
                            ?? throw new InvalidOperationException("VpnServer not found");
        var now = DateTimeOffset.UtcNow;
        await openVpnServerCommandService.UpdateWhere(
            x => x.Id == vpnServerId,
            u => u.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.LastUpdate, now),
            ct);
        await serverOpenVpnNotificationService.NotifyDeleted(openVpnServer.Id, openVpnServer.ServerName, ct);
        statusCacheGenerationService.Bump();
        microserviceClientFactory.Invalidate(openVpnServer.Id);
        eventClientFactory.Remove(openVpnServer.Id);
        return true;
    }

    public async Task<VpnServerPostSetupExecutionResult> RunPostAddSetupAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await openVpnServerQueryService.GetById(vpnServerId, ct)
                     ?? throw new InvalidOperationException("VpnServer not found");

        var createdDefaultConfig = await TryCreateDefaultExportConfigIfMissingAsync(server, ct);
        return new VpnServerPostSetupExecutionResult
        {
            VpnServerId = vpnServerId,
            ServerType = server.ServerType,
            CreatedDefaultConfig = createdDefaultConfig
        };
    }

    private const string DefaultXrayClientLinkTemplate =
        "{{vless_uri}}\r\n# {{friendly_name}}\r\nUUID: {{uuid}}\r\nEndpoint: {{server_ip}}:{{server_port}}\r\n";

    private const string DefaultOpenVpnClientConfigTemplate =
        """
        setenv FRIENDLY_NAME "{{friendly_name}}"
        client
        dev tun
        proto tcp
        remote {{server_ip}} {{server_port}}
        resolv-retry infinite
        nobind
        remote-cert-tls server
        tls-version-min 1.2
        cipher AES-256-CBC
        auth SHA256
        auth-nocache
        verb 3
        <ca>
        {{ca_cert}}
        </ca>
        <cert>
        {{client_cert}}
        </cert>
        <key>
        {{client_key}}
        </key>
        <tls-crypt>
        {{tls_auth_key}}
        </tls-crypt>
        """;

    private async Task EnsureDefaultExportConfigIfMissingAsync(VpnServer server, CancellationToken ct)
    {
        _ = await TryCreateDefaultExportConfigIfMissingAsync(server, ct);
    }

    private async Task<bool> TryCreateDefaultExportConfigIfMissingAsync(VpnServer server, CancellationToken ct)
    {
        if (server.ServerType == VpnServerType.OpenVpn)
            return await TryCreateOpenVpnDefaultExportConfigAsync(server, ct);

        if (server.ServerType == VpnServerType.Xray)
            return await TryCreateXrayDefaultExportConfigAsync(server, ct);

        return false;
    }

    private async Task<bool> TryCreateOpenVpnDefaultExportConfigAsync(VpnServer server, CancellationToken ct)
    {
        if (await openVpnServerOvpnFileConfigQueryService.AnyByVpnServerId(server.Id, ct))
            return false;

        var config = new VpnServerOvpnFileConfig
        {
            VpnServerId = server.Id,
            VpnServerIp = await GetExternalIpSafelyAsync(ct),
            ConfigTemplate = DefaultOpenVpnClientConfigTemplate,
        };

        await TryApplyDetectedOpenVpnSettingsAsync(server.Id, config, ct);
        await openVpnServerOvpnFileConfigCommandService.Add(config, true, ct);
        return true;
    }

    private async Task<bool> TryCreateXrayDefaultExportConfigAsync(VpnServer server, CancellationToken ct)
    {
        if (await openVpnServerOvpnFileConfigQueryService.AnyByVpnServerId(server.Id, ct))
            return false;

        var ip = await GetExternalIpSafelyAsync(ct);
        await openVpnServerOvpnFileConfigCommandService.Add(new VpnServerOvpnFileConfig
        {
            VpnServerId = server.Id,
            VpnServerIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip,
            VpnServerPort = 443,
            ConfigTemplate = DefaultXrayClientLinkTemplate,
        }, true, ct);
        return true;
    }
    
    private async Task SyncQuotaPlanLinksAsync(
        int vpnServerId,
        IReadOnlyCollection<int> quotaPlanIds,
        CancellationToken ct)
    {
        // Remove old links
        await quotaPlanAllowedServerCommandService.DeleteWhere(
            x => x.VpnServerId == vpnServerId,
            ct);

        // Add new links
        if (quotaPlanIds.Count == 0)
            return;

        var links = quotaPlanIds
            .Distinct()
            .Select(planId => new QuotaPlanAllowedServer
            {
                VpnServerId = vpnServerId,
                QuotaPlanId = planId
            })
            .ToList();

        await quotaPlanAllowedServerCommandService.AddRange(links, saveChanges: true, ct);
    }

    private async Task SyncTagLinksAsync(
        int vpnServerId,
        IReadOnlyCollection<int> tagIds,
        CancellationToken ct)
    {
        await openVpnServerTagCommandService.DeleteWhere(
            x => x.VpnServerId == vpnServerId,
            ct);

        if (tagIds.Count == 0)
            return;

        var links = tagIds
            .Distinct()
            .Select(tagId => new VpnServerTag
            {
                VpnServerId = vpnServerId,
                TagId = tagId
            })
            .ToList();

        await openVpnServerTagCommandService.AddRange(links, saveChanges: true, ct);
    }

    private async Task TryApplyDetectedOpenVpnSettingsAsync(int vpnServerId, VpnServerOvpnFileConfig config, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(OpenVpnAutoDetectTimeout);
            var diagnostics = await microserviceInfoService.GetInfoAsync(vpnServerId, timeoutCts.Token);
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
            logger.LogDebug(ex,
                "Failed to auto-detect default OpenVPN export config for VpnServerId={VpnServerId}.",
                vpnServerId);
        }
    }

    private async Task<string> GetExternalIpSafelyAsync(CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ExternalIpResolveTimeout);
            return await externalIpAddressService.GetRemoteIpAddress(timeoutCts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve external IP quickly; fallback to loopback value.");
            return "127.0.0.1";
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
