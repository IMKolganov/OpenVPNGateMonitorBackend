using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace DataGateMonitor.Services.Helpers;

public interface IVpnNodePublicIpLookup
{
    /// <summary>
    /// Best-effort public IP from the VPN node <c>/api/info</c>, cached to avoid polling every status cycle.
    /// </summary>
    Task<string?> GetAsync(int vpnServerId, VpnServerType serverType, CancellationToken cancellationToken);

    /// <summary>Drop cached PublicIp for a server (e.g. after ApiUrl change or delete).</summary>
    void Invalidate(int vpnServerId);
}

public sealed class VpnNodePublicIpLookup(
    IMicroserviceInfoService microserviceInfoService,
    IMemoryCache memoryCache,
    ILogger<VpnNodePublicIpLookup> logger) : IVpnNodePublicIpLookup
{
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromMinutes(2);

    internal static string CacheKey(int vpnServerId) => $"vpn-node-public-ip:{vpnServerId}";

    public async Task<string?> GetAsync(int vpnServerId, VpnServerType serverType, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(vpnServerId);
        if (memoryCache.TryGetValue(cacheKey, out string? cached))
        {
            // Empty string = resolved miss (old node / no PublicIp / transient failure).
            return string.IsNullOrEmpty(cached) ? null : cached;
        }

        string? ip = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            var info = await microserviceInfoService.GetInfoAsync(vpnServerId, timeoutCts.Token);
            ip = serverType == VpnServerType.Xray ? info.Xray?.PublicIp : info.OpenVpn?.PublicIp;
            if (!string.IsNullOrWhiteSpace(ip))
                ip = ip.Trim();
            else
                ip = null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "VpnServerId: {Id}. Could not load node PublicIp from /api/info.", vpnServerId);
            ip = null;
        }

        memoryCache.Set(cacheKey, ip ?? string.Empty, ip is null ? NegativeTtl : PositiveTtl);
        return ip;
    }

    public void Invalidate(int vpnServerId)
    {
        memoryCache.Remove(CacheKey(vpnServerId));
    }
}
