using System.Collections.Concurrent;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

/// <summary>
/// Short-lived single-flight cache for overview traffic SQL.
/// Dashboard refresh storms (summary+users+series) share one DB round-trip per key.
/// </summary>
internal static class OverviewTrafficResultCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (TryGet(key, out T hit))
            return hit;

        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (TryGet(key, out hit))
                return hit;

            var value = await factory(ct).ConfigureAwait(false);
            Entries[key] = new CacheEntry(value!, DateTimeOffset.UtcNow.Add(Ttl));
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool TryGet<T>(string key, out T value)
    {
        value = default!;
        if (!Entries.TryGetValue(key, out var entry))
            return false;
        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            Entries.TryRemove(key, out _);
            return false;
        }

        if (entry.Value is not T typed)
            return false;

        value = typed;
        return true;
    }

    private readonly record struct CacheEntry(object Value, DateTimeOffset ExpiresAtUtc);
}
