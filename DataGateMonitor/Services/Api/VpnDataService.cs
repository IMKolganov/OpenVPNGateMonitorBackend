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
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.Api.PostSetup;
using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Services.Api;

public class VpnDataService(
    ILogger<IVpnDataService> logger,
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
    IOpenVpnEventClientFactory eventClientFactory,
    IVpnNodePublicIpLookup vpnNodePublicIpLookup,
    IVpnServerClientPresenceService vpnServerClientPresenceService) : IVpnDataService
{
    private static readonly TimeSpan MicroserviceInfoResolveTimeout = TimeSpan.FromSeconds(5);

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
        var previous = await openVpnServerQueryService.GetById(server.Id, ct)
                       ?? throw new InvalidOperationException("OpenVPN server not found");
        var becameDisabled = server.IsDisable && !previous.IsDisable;
        var apiUrlChanged = !string.Equals(
            previous.ApiUrl?.Trim(), server.ApiUrl?.Trim(), StringComparison.OrdinalIgnoreCase);

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

            // UpdateServerRequest does not carry list layout fields — preserve them.
            server.VpnServerGroupId = previous.VpnServerGroupId;
            server.SortOrder = previous.SortOrder;
            server.CreateDate = previous.CreateDate;
            server.IsDeleted = previous.IsDeleted;
            server.DcoIsEnabled = previous.DcoIsEnabled;
            server.XrayClientsPolledAt = previous.XrayClientsPolledAt;
            server.XrayClientsPollError = previous.XrayClientsPollError;

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
        if (apiUrlChanged)
            vpnNodePublicIpLookup.Invalidate(result.Id);
        if (becameDisabled)
            await vpnServerClientPresenceService.MarkAllDisconnectedAsync(result.Id, ct);
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
        await vpnServerClientPresenceService.MarkAllDisconnectedAsync(vpnServerId, ct);
        await serverOpenVpnNotificationService.NotifyDeleted(openVpnServer.Id, openVpnServer.ServerName, ct);
        statusCacheGenerationService.Bump();
        microserviceClientFactory.Invalidate(openVpnServer.Id);
        eventClientFactory.Remove(openVpnServer.Id);
        vpnNodePublicIpLookup.Invalidate(openVpnServer.Id);
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

    /// <summary>Keep in sync with frontend <c>XRAY_EXPORT_TEMPLATE</c> and xray ClientLinkServiceDnsPlaceholderTests.</summary>
    private const string DefaultXrayClientLinkTemplate =
        """
        {
          "vless": "{{vless_uri}}",
          "dnsServers": {{dns_servers_json}},
          "dnsIdentityEnabled": {{dns_identity_enabled}},
          "friendlyName": "{{friendly_name}}",
          "uuid": "{{uuid}}",
          "endpoint": "{{server_ip}}:{{server_port}}"
        }
        """;

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

        // Empty IP until node /api/info PublicIp is applied — never dashboard WAN IP.
        var config = new VpnServerOvpnFileConfig
        {
            VpnServerId = server.Id,
            VpnServerIp = string.Empty,
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

        var ip = await TryGetNodePublicIpAsync(server.Id, VpnServerType.Xray, ct) ?? string.Empty;
        await openVpnServerOvpnFileConfigCommandService.Add(new VpnServerOvpnFileConfig
        {
            VpnServerId = server.Id,
            VpnServerIp = ip,
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

    private Task TryApplyDetectedOpenVpnSettingsAsync(int vpnServerId, VpnServerOvpnFileConfig config, CancellationToken ct) =>
        OpenVpnDetectedSettingsHelper.TryApplyAsync(
            vpnServerId,
            config,
            microserviceInfoService,
            logger,
            "Failed to auto-detect default OpenVPN export config for VpnServerId={VpnServerId}.",
            ct,
            MicroserviceInfoResolveTimeout);

    private async Task<string?> TryGetNodePublicIpAsync(int vpnServerId, VpnServerType serverType, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(MicroserviceInfoResolveTimeout);
            var diagnostics = await microserviceInfoService.GetInfoAsync(vpnServerId, timeoutCts.Token);
            var ip = serverType == VpnServerType.Xray
                ? diagnostics.Xray?.PublicIp
                : diagnostics.OpenVpn?.PublicIp;
            return string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "VpnServerId: {Id}. Could not load node PublicIp for default export config.", vpnServerId);
            return null;
        }
    }
}
