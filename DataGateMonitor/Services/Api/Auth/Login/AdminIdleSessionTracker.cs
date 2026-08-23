using Microsoft.Extensions.Caching.Memory;

namespace DataGateMonitor.Services.Api.Auth.Login;

public sealed class AdminIdleSessionTracker(
    IAdminIdleTimeoutProvider idleTimeoutProvider,
    IMemoryCache memoryCache)
    : IAdminIdleSessionTracker
{
    private const string AdminRole = "Admin";

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(idleTimeoutProvider.GetMinutes());

    public void Touch(int userId)
    {
        if (userId <= 0) return;

        var timeout = IdleTimeout;
        var cacheKey = CacheKey(userId);
        memoryCache.Set(cacheKey, DateTimeOffset.UtcNow, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = timeout.Add(timeout),
        });
    }

    public bool IsExpired(int userId)
    {
        if (userId <= 0) return true;

        if (!memoryCache.TryGetValue(CacheKey(userId), out DateTimeOffset lastActivity))
            return true;

        return DateTimeOffset.UtcNow - lastActivity >= IdleTimeout;
    }

    public void Clear(int userId)
    {
        if (userId <= 0) return;
        memoryCache.Remove(CacheKey(userId));
    }

    public static bool IsAdminRole(string? role) =>
        string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase);

    private static string CacheKey(int userId) => $"admin-session-idle:{userId}";
}
