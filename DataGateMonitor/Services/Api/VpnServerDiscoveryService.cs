using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.VpnServerConflogTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Api;

public sealed class VpnServerDiscoveryService(
    IQueryService<VpnServerDiscovery, int> discoveryQuery,
    ICommandService<VpnServerDiscovery, int> discoveryCommand,
    IQueryService<VpnServer, int> vpnServerQuery,
    IVpnServerOvpnFileConfigQueryService ovpnFileConfigQuery,
    IVpnServerConflogQueryService conflogQuery,
    IVpnDataService vpnDataService,
    IServerOpenVpnNotificationService serverOpenVpnNotificationService,
    IMemoryCache cache,
    ILogger<VpnServerDiscoveryService> logger) : IVpnServerDiscoveryService
{
    public const string RateLimitMessage = "Too many discovery announces. Try again later.";
    public const string NotFoundMessage = "Server discovery not found.";
    public const string NotPendingMessage = "Server discovery is no longer pending.";

    private static readonly TimeSpan ReNotifyCooldown = TimeSpan.FromHours(24);
    private static readonly TimeSpan DnsLookupTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DnsCacheTtl = TimeSpan.FromMinutes(10);
    private const int AnnounceRateLimitMax = 30;
    private const int AnnounceRateLimitWindowMinutes = 10;

    public bool TryAcquireAnnounceSlot(string? clientIp)
    {
        var key = $"vpn-discover:rl:{(string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim())}";
        var window = TimeSpan.FromMinutes(AnnounceRateLimitWindowMinutes);
        if (!cache.TryGetValue(key, out RateLimitEntry? entry) || entry is null)
        {
            entry = new RateLimitEntry { Count = 1 };
            cache.Set(key, entry, window);
            return true;
        }

        if (entry.Count >= AnnounceRateLimitMax)
            return false;

        entry.Count++;
        return true;
    }

    public async Task<AnnounceVpnServerResponse> AnnounceAsync(AnnounceVpnServerRequest request, CancellationToken ct)
    {
        var apiUrl = VpnServerApiUrlHelper.NormalizeApiUrl(request.ApiUrl);
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("ApiUrl is required.");

        var existingServer = await FindExistingServerAsync(apiUrl, request.ServerType, request.PublicIp, ct);

        if (existingServer is not null)
        {
            await ResolveStalePendingDiscoveriesForServerAsync(existingServer, ct);
            return new AnnounceVpnServerResponse
            {
                Status = AnnounceVpnServerResultStatus.AlreadyRegistered,
                ExistingVpnServerId = existingServer.Id
            };
        }

        var now = DateTimeOffset.UtcNow;
        var pendingDiscoveries = await discoveryQuery.Query(asNoTracking: false)
            .Where(d => d.Status == VpnServerDiscoveryStatus.Pending)
            .ToListAsync(ct);
        VpnServerDiscovery? pending = null;
        foreach (var candidate in pendingDiscoveries)
        {
            if (await AnnounceMatchesDiscoveryAsync(apiUrl, request.ServerType, request.PublicIp, candidate, ct))
            {
                pending = candidate;
                break;
            }
        }

        // A previously rejected discovery for the same URL: keep reporting Rejected (do not reopen).
        if (pending is null)
        {
            var rejectedCandidates = await discoveryQuery.Query()
                .Where(d => d.Status == VpnServerDiscoveryStatus.Rejected)
                .OrderByDescending(d => d.RejectedAt)
                .ToListAsync(ct);
            foreach (var candidate in rejectedCandidates)
            {
                if (await AnnounceMatchesDiscoveryAsync(apiUrl, request.ServerType, request.PublicIp, candidate, ct))
                {
                    return new AnnounceVpnServerResponse
                    {
                        Status = AnnounceVpnServerResultStatus.Rejected,
                        DiscoveryId = candidate.Id
                    };
                }
            }
        }

        if (pending is null)
        {
            pending = new VpnServerDiscovery
            {
                ServerType = request.ServerType,
                ApiUrl = apiUrl,
                SuggestedName = Truncate(request.SuggestedName?.Trim(), 128),
                PublicIp = Truncate(request.PublicIp?.Trim(), 64),
                Version = Truncate(request.Version?.Trim(), 64),
                IsEnableWss = request.IsEnableWss,
                Status = VpnServerDiscoveryStatus.Pending,
                LastSeenUtc = now,
            };
            await discoveryCommand.Add(pending, ct: ct);
            await NotifyDiscoveredAsync(pending, now, ct);
            return new AnnounceVpnServerResponse
            {
                Status = AnnounceVpnServerResultStatus.Pending,
                DiscoveryId = pending.Id
            };
        }

        pending.ServerType = request.ServerType;
        pending.SuggestedName = Truncate(request.SuggestedName?.Trim(), 128) ?? pending.SuggestedName;
        pending.PublicIp = Truncate(request.PublicIp?.Trim(), 64) ?? pending.PublicIp;
        pending.Version = Truncate(request.Version?.Trim(), 64) ?? pending.Version;
        pending.IsEnableWss = request.IsEnableWss;
        pending.LastSeenUtc = now;
        await discoveryCommand.Update(pending, ct: ct);

        var shouldNotify = pending.LastNotifiedUtc is null
            || now - pending.LastNotifiedUtc.Value >= ReNotifyCooldown;
        if (shouldNotify)
            await NotifyDiscoveredAsync(pending, now, ct);

        return new AnnounceVpnServerResponse
        {
            Status = AnnounceVpnServerResultStatus.Pending,
            DiscoveryId = pending.Id
        };
    }

    public async Task<VpnServerDiscoveriesResponse> ListPendingAsync(CancellationToken ct)
    {
        var items = await discoveryQuery.Query(asNoTracking: false)
            .Where(d => d.Status == VpnServerDiscoveryStatus.Pending)
            .OrderByDescending(d => d.LastSeenUtc)
            .ToListAsync(ct);

        var activeServers = await vpnServerQuery.Query()
            .Where(s => !s.IsDeleted)
            .ToListAsync(ct);
        var serverContexts = await BuildServerMatchContextsAsync(activeServers, ct);

        var visible = new List<VpnServerDiscovery>();
        foreach (var item in items)
        {
            var match = await TryFindSingleMatchingServerAsync(
                activeServers,
                serverContexts,
                VpnServerApiUrlHelper.NormalizeApiUrl(item.ApiUrl),
                item.ServerType,
                item.PublicIp,
                ct);

            if (match is not null)
            {
                await ResolveStalePendingDiscoveryAsync(item, match.Id, ct);
                continue;
            }

            visible.Add(item);
        }

        return new VpnServerDiscoveriesResponse
        {
            Discoveries = visible.Select(ToDto).ToList()
        };
    }

    public async Task<VpnServerDiscoveryResponse> ApproveAsync(
        int discoveryId,
        ApproveVpnServerDiscoveryRequest request,
        CancellationToken ct)
    {
        var discovery = await discoveryQuery.FindById(discoveryId, asNoTracking: false, ct: ct)
            ?? throw new InvalidOperationException(NotFoundMessage);

        if (discovery.Status != VpnServerDiscoveryStatus.Pending)
            throw new InvalidOperationException(NotPendingMessage);

        var serverName = !string.IsNullOrWhiteSpace(request.ServerName)
            ? request.ServerName.Trim()
            : (!string.IsNullOrWhiteSpace(discovery.SuggestedName)
                ? discovery.SuggestedName.Trim()
                : DeriveNameFromApiUrl(discovery.ApiUrl));

        var server = new VpnServer
        {
            ServerType = discovery.ServerType,
            ServerName = serverName,
            ApiUrl = discovery.ApiUrl,
            IsDefault = request.IsDefault,
            IsEnableWss = request.IsEnableWss || discovery.IsEnableWss,
            IsPiHoleEnabled = request.IsPiHoleEnabled,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsDisable = request.IsDisabled,
            IsOnline = false,
        };

        var created = await vpnDataService.AddVpnServer(
            server,
            request.QuotaPlanIds,
            request.TagIds,
            ct);

        discovery.Status = VpnServerDiscoveryStatus.Approved;
        discovery.ResolvedVpnServerId = created.Id;
        await discoveryCommand.Update(discovery, ct: ct);

        logger.LogInformation(
            "Approved VPN server discovery {DiscoveryId} as VpnServer {VpnServerId} ({ApiUrl})",
            discovery.Id, created.Id, discovery.ApiUrl);

        return new VpnServerDiscoveryResponse
        {
            Discovery = ToDto(discovery),
            VpnServerId = created.Id
        };
    }

    public async Task<VpnServerDiscoveryResponse> DenyAsync(
        int discoveryId,
        DenyVpnServerDiscoveryRequest? request,
        CancellationToken ct)
    {
        var discovery = await discoveryQuery.FindById(discoveryId, asNoTracking: false, ct: ct)
            ?? throw new InvalidOperationException(NotFoundMessage);

        if (discovery.Status != VpnServerDiscoveryStatus.Pending)
            throw new InvalidOperationException(NotPendingMessage);

        discovery.Status = VpnServerDiscoveryStatus.Rejected;
        discovery.RejectedAt = DateTimeOffset.UtcNow;
        discovery.RejectReason = Truncate(request?.Reason?.Trim(), 512);
        await discoveryCommand.Update(discovery, ct: ct);

        logger.LogInformation("Denied VPN server discovery {DiscoveryId} ({ApiUrl})", discovery.Id, discovery.ApiUrl);

        return new VpnServerDiscoveryResponse
        {
            Discovery = ToDto(discovery)
        };
    }

    private async Task<VpnServer?> FindExistingServerAsync(
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        var activeServers = await vpnServerQuery.Query()
            .Where(s => !s.IsDeleted)
            .ToListAsync(ct);

        var serverContexts = await BuildServerMatchContextsAsync(activeServers, ct);
        return await TryFindSingleMatchingServerAsync(
            activeServers,
            serverContexts,
            normalizedApiUrl,
            serverType,
            publicIp,
            ct);
    }

    private enum DiscoveryMatchStrength
    {
        None,
        Secondary,
        ExactUrl
    }

    private async Task<VpnServer?> TryFindSingleMatchingServerAsync(
        IReadOnlyList<VpnServer> candidates,
        IReadOnlyDictionary<int, ServerMatchContext> contexts,
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        VpnServer? exactMatch = null;
        List<VpnServer>? secondaryMatches = null;

        foreach (var server in candidates)
        {
            var strength = await GetMatchStrengthAsync(
                server, contexts, normalizedApiUrl, serverType, publicIp, ct);
            if (strength == DiscoveryMatchStrength.None)
                continue;

            if (strength == DiscoveryMatchStrength.ExactUrl)
            {
                if (exactMatch is not null)
                {
                    logger.LogDebug(
                        "Ambiguous exact ApiUrl discovery match for {ApiUrl} ({ServerType}): servers {FirstId} and {SecondId}.",
                        normalizedApiUrl, serverType, exactMatch.Id, server.Id);
                    return null;
                }

                exactMatch = server;
                continue;
            }

            (secondaryMatches ??= []).Add(server);
        }

        if (exactMatch is not null)
            return exactMatch;

        if (secondaryMatches is null || secondaryMatches.Count == 0)
            return null;

        if (secondaryMatches.Count > 1)
        {
            logger.LogDebug(
                "Ambiguous VPN discovery match for {ApiUrl} ({ServerType}, {PublicIp}): servers {FirstId} and {SecondId}.",
                normalizedApiUrl, serverType, publicIp, secondaryMatches[0].Id, secondaryMatches[1].Id);
            return null;
        }

        return secondaryMatches[0];
    }

    private async Task<DiscoveryMatchStrength> GetMatchStrengthAsync(
        VpnServer server,
        IReadOnlyDictionary<int, ServerMatchContext> contexts,
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        if (server.ServerType != serverType)
            return DiscoveryMatchStrength.None;

        if (VpnServerApiUrlHelper.ApiUrlsEquivalent(server.ApiUrl, normalizedApiUrl))
            return DiscoveryMatchStrength.ExactUrl;

        if (await HasSecondaryMatchAsync(server, contexts, normalizedApiUrl, serverType, publicIp, ct))
            return DiscoveryMatchStrength.Secondary;

        return DiscoveryMatchStrength.None;
    }

    private async Task<bool> HasSecondaryMatchAsync(
        VpnServer server,
        IReadOnlyDictionary<int, ServerMatchContext> contexts,
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        if (CanCompareByResolvedEndpoint(normalizedApiUrl, server.ApiUrl))
        {
            var announceEndpoint = await BuildEndpointKeyAsync(normalizedApiUrl, serverType, publicIp, ct);
            var serverEndpoint = await BuildEndpointKeyAsync(server.ApiUrl, server.ServerType, publicIp: null, ct);
            if (announceEndpoint is not null && announceEndpoint == serverEndpoint)
                return true;
        }

        var announceNode = await BuildAnnounceNodeIdentityKeyAsync(normalizedApiUrl, serverType, publicIp, ct);
        var serverNode = await BuildServerNodeIdentityKeyAsync(server, contexts, ct);
        return announceNode is not null && announceNode == serverNode;
    }

    private async Task<bool> ServerMatchesAnnounceAsync(
        VpnServer server,
        IReadOnlyDictionary<int, ServerMatchContext> contexts,
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct) =>
        await GetMatchStrengthAsync(server, contexts, normalizedApiUrl, serverType, publicIp, ct)
            != DiscoveryMatchStrength.None;

    private async Task<bool> AnnounceMatchesDiscoveryAsync(
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        VpnServerDiscovery discovery,
        CancellationToken ct)
    {
        if (discovery.ServerType != serverType)
            return false;

        if (VpnServerApiUrlHelper.ApiUrlsEquivalent(discovery.ApiUrl, normalizedApiUrl))
            return true;

        if (CanCompareByResolvedEndpoint(normalizedApiUrl, discovery.ApiUrl))
        {
            var announceEndpoint = await BuildEndpointKeyAsync(normalizedApiUrl, serverType, publicIp, ct);
            var discoveryEndpoint = await BuildEndpointKeyAsync(
                discovery.ApiUrl, discovery.ServerType, discovery.PublicIp, ct);
            if (announceEndpoint is not null && announceEndpoint == discoveryEndpoint)
                return true;
        }

        var announceNode = await BuildAnnounceNodeIdentityKeyAsync(normalizedApiUrl, serverType, publicIp, ct);
        var discoveryNode = BuildDiscoveryNodeIdentityKey(discovery);
        return announceNode is not null && announceNode == discoveryNode;
    }

    /// <summary>
    /// Layer-2 (DNS IP + URL port) is unsafe for nginx-fronted default ports (:443/:80):
    /// multiple stacks on one host would share the same endpoint key.
    /// </summary>
    private static bool CanCompareByResolvedEndpoint(string? announceApiUrl, string? otherApiUrl) =>
        !VpnServerApiUrlHelper.UsesSchemeDefaultPort(announceApiUrl)
        && !VpnServerApiUrlHelper.UsesSchemeDefaultPort(otherApiUrl);

    private sealed record ServerMatchContext(
        VpnServerOvpnFileConfig? OvpnConfig,
        VpnServerConflog? LastConflog);

    private async Task<IReadOnlyDictionary<int, ServerMatchContext>> BuildServerMatchContextsAsync(
        IReadOnlyList<VpnServer> servers,
        CancellationToken ct)
    {
        var contexts = new Dictionary<int, ServerMatchContext>(servers.Count);
        foreach (var server in servers)
        {
            var ovpnConfig = await ovpnFileConfigQuery.GetByVpnServerIdId(server.Id, ct);
            var lastConflog = await conflogQuery.GetLastByVpnServerId(server.Id, ct);
            contexts[server.Id] = new ServerMatchContext(ovpnConfig, lastConflog);
        }

        return contexts;
    }

    private async Task<VpnServerApiUrlHelper.ManagerEndpointKey?> BuildAnnounceNodeIdentityKeyAsync(
        string normalizedApiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        if (!VpnServerApiUrlHelper.TryGetEndpointHostPort(normalizedApiUrl, out var host, out var managerApiPort))
            return null;

        var hostIp = VpnServerApiUrlHelper.TryParseHostIp(host)
            ?? await ResolveHostIpAsync(host, ct);
        var resolvedPublicIp = VpnServerNodeIdentityResolver.TryResolvePublicIp(
            vpnServerIp: null,
            announcePublicIp: publicIp,
            apiUrlHostIp: hostIp);

        return VpnServerNodeIdentityResolver.BuildNodeIdentityKey(resolvedPublicIp, managerApiPort, serverType);
    }

    private async Task<VpnServerApiUrlHelper.ManagerEndpointKey?> BuildServerNodeIdentityKeyAsync(
        VpnServer server,
        IReadOnlyDictionary<int, ServerMatchContext> contexts,
        CancellationToken ct)
    {
        contexts.TryGetValue(server.Id, out var context);
        var managerApiPort = VpnServerNodeIdentityResolver.TryResolveManagerApiPort(
            server.ApiUrl,
            server.ServerType,
            context?.LastConflog?.PayloadJson);

        string? resolvedPublicIp = null;
        if (context?.OvpnConfig is not null)
        {
            resolvedPublicIp = VpnServerNodeIdentityResolver.TryResolvePublicIp(
                context.OvpnConfig.VpnServerIp,
                announcePublicIp: null,
                apiUrlHostIp: null);
        }

        if (resolvedPublicIp is null
            && VpnServerApiUrlHelper.TryGetEndpointHostPort(server.ApiUrl, out var host, out _))
        {
            resolvedPublicIp = VpnServerApiUrlHelper.TryParseHostIp(host)
                ?? await ResolveHostIpAsync(host, ct);
        }

        return VpnServerNodeIdentityResolver.BuildNodeIdentityKey(
            resolvedPublicIp,
            managerApiPort,
            server.ServerType);
    }

    private static VpnServerApiUrlHelper.ManagerEndpointKey? BuildDiscoveryNodeIdentityKey(VpnServerDiscovery discovery)
    {
        if (!VpnServerApiUrlHelper.TryGetEndpointHostPort(discovery.ApiUrl, out _, out var managerApiPort))
            return null;

        var publicIp = VpnServerNodeIdentityResolver.TryResolvePublicIp(
            vpnServerIp: null,
            announcePublicIp: discovery.PublicIp,
            apiUrlHostIp: VpnServerApiUrlHelper.TryParsePublicIp(
                VpnServerApiUrlHelper.TryHostFromApiUrl(discovery.ApiUrl)));

        return VpnServerNodeIdentityResolver.BuildNodeIdentityKey(publicIp, managerApiPort, discovery.ServerType);
    }

    private async Task<VpnServerApiUrlHelper.ManagerEndpointKey?> BuildEndpointKeyAsync(
        string? apiUrl,
        VpnServerType serverType,
        string? publicIp,
        CancellationToken ct)
    {
        if (!VpnServerApiUrlHelper.TryGetEndpointHostPort(apiUrl, out var host, out _))
            return null;

        var resolvedIp = VpnServerApiUrlHelper.TryParseHostIp(host)
            ?? await ResolveHostIpAsync(host, ct);
        if (resolvedIp is null
            && !string.IsNullOrWhiteSpace(publicIp)
            && VpnServerApiUrlHelper.TryParseHostIp(publicIp) is { } hintedIp
            && string.Equals(host, publicIp.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            resolvedIp = hintedIp;
        }

        return VpnServerApiUrlHelper.BuildEndpointKey(apiUrl, serverType, resolvedIp);
    }

    private async Task<string?> ResolveHostIpAsync(string host, CancellationToken ct)
    {
        var cacheKey = $"vpn-discover:dns:{host.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out string? cachedIp))
            return cachedIp;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(DnsLookupTimeout);
            var entries = await Dns.GetHostAddressesAsync(host, timeout.Token);
            var chosen = entries.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                         ?? entries.FirstOrDefault();
            if (chosen is null)
                return null;

            var ip = chosen.IsIPv4MappedToIPv6 ? chosen.MapToIPv4().ToString() : chosen.ToString();
            cache.Set(cacheKey, ip, DnsCacheTtl);
            return ip;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "DNS lookup failed for VPN manager host {Host}", host);
            return null;
        }
    }

    private async Task ResolveStalePendingDiscoveriesForServerAsync(VpnServer registered, CancellationToken ct)
    {
        var contexts = await BuildServerMatchContextsAsync([registered], ct);
        var stale = await discoveryQuery.Query(asNoTracking: false)
            .Where(d => d.Status == VpnServerDiscoveryStatus.Pending)
            .ToListAsync(ct);

        foreach (var item in stale)
        {
            if (await ServerMatchesAnnounceAsync(
                    registered,
                    contexts,
                    VpnServerApiUrlHelper.NormalizeApiUrl(item.ApiUrl),
                    item.ServerType,
                    item.PublicIp,
                    ct))
            {
                await ResolveStalePendingDiscoveryAsync(item, registered.Id, ct);
            }
        }
    }

    private async Task ResolveStalePendingDiscoveryAsync(VpnServerDiscovery discovery, int vpnServerId, CancellationToken ct)
    {
        discovery.Status = VpnServerDiscoveryStatus.Approved;
        discovery.ResolvedVpnServerId = vpnServerId;
        await discoveryCommand.Update(discovery, ct: ct);

        logger.LogInformation(
            "Auto-resolved stale VPN server discovery {DiscoveryId} — already registered as VpnServer {VpnServerId} ({ApiUrl})",
            discovery.Id, vpnServerId, discovery.ApiUrl);
    }

    private async Task NotifyDiscoveredAsync(VpnServerDiscovery discovery, DateTimeOffset now, CancellationToken ct)
    {
        await serverOpenVpnNotificationService.NotifyDiscovered(
            discovery.Id,
            discovery.SuggestedName,
            discovery.ApiUrl,
            ct);
        discovery.LastNotifiedUtc = now;
        await discoveryCommand.Update(discovery, ct: ct);
    }

    private static VpnServerDiscoveryDto ToDto(VpnServerDiscovery d) => new()
    {
        Id = d.Id,
        ServerType = d.ServerType,
        ApiUrl = d.ApiUrl,
        SuggestedName = d.SuggestedName,
        PublicIp = d.PublicIp,
        Version = d.Version,
        IsEnableWss = d.IsEnableWss,
        Status = d.Status,
        CreatedAt = d.CreateDate,
        LastSeenUtc = d.LastSeenUtc,
        ResolvedVpnServerId = d.ResolvedVpnServerId
    };

    private static string DeriveNameFromApiUrl(string apiUrl)
    {
        if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return Truncate(uri.Host, 128) ?? "discovered-server";
        return "discovered-server";
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }

    private sealed class RateLimitEntry
    {
        public int Count { get; set; }
    }
}
