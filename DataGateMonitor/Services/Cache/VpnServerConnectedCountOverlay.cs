using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

namespace DataGateMonitor.Services.Cache;

/// <summary>
/// Overlays live Redis connected-client counters onto status DTOs so HTTP cache
/// can keep expensive session aggregates while connected counts stay fresh.
/// </summary>
public static class VpnServerConnectedCountOverlay
{
    public static async Task ApplyAsync(
        IEnumerable<VpnServerWithStatusDto> items,
        IConnectedClientsCounterStore store,
        CancellationToken ct = default)
    {
        var list = items as IList<VpnServerWithStatusDto> ?? items.ToList();
        if (list.Count == 0)
            return;

        var ids = list
            .Select(x => x.VpnServerResponses?.VpnServer?.Id ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
            return;

        var map = await store.GetManyAsync(ids, ct);
        if (map is null || map.Count == 0)
            return;

        foreach (var item in list)
        {
            var id = item.VpnServerResponses?.VpnServer?.Id ?? 0;
            if (id > 0 && map.TryGetValue(id, out var count))
                item.CountConnectedClients = count;
        }
    }

    public static async Task ApplyAsync(
        IEnumerable<VpnServerWithStatusV2Dto> items,
        IConnectedClientsCounterStore store,
        CancellationToken ct = default)
    {
        var list = items as IList<VpnServerWithStatusV2Dto> ?? items.ToList();
        if (list.Count == 0)
            return;

        var ids = list
            .Select(x => x.VpnServerResponses?.VpnServer?.Id ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
            return;

        var map = await store.GetManyAsync(ids, ct);
        if (map is null || map.Count == 0)
            return;

        foreach (var item in list)
        {
            var id = item.VpnServerResponses?.VpnServer?.Id ?? 0;
            if (id > 0 && map.TryGetValue(id, out var count))
                item.CountConnectedClients = count;
        }
    }
}
