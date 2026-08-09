using StackExchange.Redis;

namespace DataGateMonitor.Services.Cache;

public sealed class RedisConnectedClientsCounterStore(
    IRedisDatabaseProvider redisDatabaseProvider,
    ILogger<RedisConnectedClientsCounterStore> logger) : IConnectedClientsCounterStore
{
    internal const string KeyPrefix = "vpn:connected-clients:";
    internal static readonly TimeSpan CounterTtl = TimeSpan.FromHours(2);

    public async Task<Dictionary<int, int>> GetManyAsync(IEnumerable<int> vpnServerIds, CancellationToken ct = default)
    {
        var ids = vpnServerIds.Distinct().ToArray();
        var result = new Dictionary<int, int>(ids.Length);
        if (ids.Length == 0) return result;

        var db = await redisDatabaseProvider.GetDatabaseAsync(ct);
        if (db is null) return result;

        try
        {
            var keys = ids.Select(id => (RedisKey)$"{KeyPrefix}{id}").ToArray();
            var values = await db.StringGetAsync(keys);
            for (var i = 0; i < ids.Length; i += 1)
            {
                if (!values[i].HasValue) continue;
                if (!int.TryParse(values[i].ToString(), out var parsed)) continue;
                result[ids[i]] = Math.Max(0, parsed);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Redis read failed for connected-clients counters");
        }

        return result;
    }

    public async Task SetAsync(int vpnServerId, int connectedClientsCount, CancellationToken ct = default)
    {
        if (vpnServerId <= 0) return;

        var db = await redisDatabaseProvider.GetDatabaseAsync(ct);
        if (db is null) return;

        try
        {
            var key = (RedisKey)$"{KeyPrefix}{vpnServerId}";
            await db.StringSetAsync(
                key,
                Math.Max(0, connectedClientsCount),
                CounterTtl,
                When.Always,
                CommandFlags.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Redis write failed for VpnServerId={VpnServerId}", vpnServerId);
        }
    }
}
