using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
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
    IVpnDataService vpnDataService,
    IServerOpenVpnNotificationService serverOpenVpnNotificationService,
    IMemoryCache cache,
    ILogger<VpnServerDiscoveryService> logger) : IVpnServerDiscoveryService
{
    public const string RateLimitMessage = "Too many discovery announces. Try again later.";
    public const string NotFoundMessage = "Server discovery not found.";
    public const string NotPendingMessage = "Server discovery is no longer pending.";

    private static readonly TimeSpan ReNotifyCooldown = TimeSpan.FromHours(24);
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
        var apiUrl = NormalizeApiUrl(request.ApiUrl);
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("ApiUrl is required.");

        var existingServer = await vpnServerQuery.Query()
            .Where(s => !s.IsDeleted)
            .FirstOrDefaultAsync(s => s.ApiUrl.ToLower() == apiUrl.ToLower(), ct);

        if (existingServer is not null)
        {
            return new AnnounceVpnServerResponse
            {
                Status = AnnounceVpnServerResultStatus.AlreadyRegistered,
                ExistingVpnServerId = existingServer.Id
            };
        }

        var now = DateTimeOffset.UtcNow;
        var pending = await discoveryQuery.Query(asNoTracking: false)
            .FirstOrDefaultAsync(
                d => d.Status == VpnServerDiscoveryStatus.Pending
                     && d.ApiUrl.ToLower() == apiUrl.ToLower(),
                ct);

        // A previously rejected discovery for the same URL: keep reporting Rejected (do not reopen).
        if (pending is null)
        {
            var rejected = await discoveryQuery.Query()
                .Where(d => d.Status == VpnServerDiscoveryStatus.Rejected
                            && d.ApiUrl.ToLower() == apiUrl.ToLower())
                .OrderByDescending(d => d.RejectedAt)
                .FirstOrDefaultAsync(ct);
            if (rejected is not null)
            {
                return new AnnounceVpnServerResponse
                {
                    Status = AnnounceVpnServerResultStatus.Rejected,
                    DiscoveryId = rejected.Id
                };
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
        var items = await discoveryQuery.Query()
            .Where(d => d.Status == VpnServerDiscoveryStatus.Pending)
            .OrderByDescending(d => d.LastSeenUtc)
            .ToListAsync(ct);

        return new VpnServerDiscoveriesResponse
        {
            Discoveries = items.Select(ToDto).ToList()
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

    internal static string NormalizeApiUrl(string? raw)
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
