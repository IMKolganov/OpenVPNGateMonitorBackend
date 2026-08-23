using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace DataGateMonitor.Services.Api.Auth;

/// <summary>
/// In-memory throttle for <c>POST /api/auth/token</c> (client-credentials).
/// Limits by client IP and by ClientId to slow brute-force attempts.
/// </summary>
public interface IAppClientTokenRateLimiter
{
    /// <returns><c>true</c> if the request is allowed; <c>false</c> if rate-limited.</returns>
    bool TryAcquire(string? clientIp, string? clientId);
}

public sealed class AppClientTokenRateLimiter(IMemoryCache cache) : IAppClientTokenRateLimiter
{
    public const string RateLimitMessage = "Too many token requests. Try again later.";

    private const int IpMaxRequests = 60;
    private const int ClientIdMaxRequests = 30;
    private const int WindowMinutes = 10;

    private static readonly ConcurrentDictionary<string, object> Locks = new();

    public bool TryAcquire(string? clientIp, string? clientId)
    {
        var ipKey = $"apptoken:rl:ip:{NormalizeIp(clientIp)}";
        string? clientKey = null;
        if (!string.IsNullOrWhiteSpace(clientId))
            clientKey = $"apptoken:rl:cid:{clientId.Trim()}";

        Record(ipKey);
        if (clientKey != null)
            Record(clientKey);

        if (IsLimited(ipKey, IpMaxRequests))
            return false;
        if (clientKey != null && IsLimited(clientKey, ClientIdMaxRequests))
            return false;

        return true;
    }

    private static string NormalizeIp(string? clientIp)
        => string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp.Trim();

    private bool IsLimited(string cacheKey, int maxRequests)
    {
        var gate = Locks.GetOrAdd(cacheKey, static _ => new object());
        lock (gate)
        {
            if (!cache.TryGetValue(cacheKey, out RateLimitEntry? entry) || entry is null)
                return false;
            // Count already includes the current attempt (Record runs first).
            return entry.Count > maxRequests;
        }
    }

    private void Record(string cacheKey)
    {
        var gate = Locks.GetOrAdd(cacheKey, static _ => new object());
        lock (gate)
        {
            if (!cache.TryGetValue(cacheKey, out RateLimitEntry? entry) || entry is null)
            {
                entry = new RateLimitEntry { Count = 1 };
                cache.Set(cacheKey, entry, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(WindowMinutes),
                });
                return;
            }

            entry.Count++;
            cache.Set(cacheKey, entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(WindowMinutes),
            });
        }
    }

    private sealed class RateLimitEntry
    {
        public int Count { get; set; }
    }
}
