using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.VpnManagerReleases;

public interface IVpnManagerUpdateStatusEnricher
{
    Task EnrichAsync(IList<VpnServerWithStatusDto> items, CancellationToken cancellationToken);

    Task EnrichAsync(IList<VpnServerWithStatusV2Dto> items, CancellationToken cancellationToken);
}

public sealed class VpnManagerUpdateStatusEnricher(
    IVpnManagerReleaseLatestService releaseLatestService) : IVpnManagerUpdateStatusEnricher
{
    public async Task EnrichAsync(IList<VpnServerWithStatusDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        var latestByType = await LoadLatestByTypeAsync(
            items.Select(i => i.VpnServerResponses.VpnServer.ServerType),
            cancellationToken);

        foreach (var item in items)
        {
            Apply(
                item.VpnServerResponses.VpnServer.ServerType,
                item.InstalledManagerVersion,
                latestByType,
                out var installed,
                out var latest,
                out var available,
                out var url);
            item.InstalledManagerVersion = installed;
            item.LatestManagerVersion = latest;
            item.IsManagerUpdateAvailable = available;
            item.ManagerReleaseUrl = url;
        }
    }

    public async Task EnrichAsync(IList<VpnServerWithStatusV2Dto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        var latestByType = await LoadLatestByTypeAsync(
            items.Select(i => i.VpnServerResponses.VpnServer.ServerType),
            cancellationToken);

        foreach (var item in items)
        {
            Apply(
                item.VpnServerResponses.VpnServer.ServerType,
                item.InstalledManagerVersion,
                latestByType,
                out var installed,
                out var latest,
                out var available,
                out var url);
            item.InstalledManagerVersion = installed;
            item.LatestManagerVersion = latest;
            item.IsManagerUpdateAvailable = available;
            item.ManagerReleaseUrl = url;
        }
    }

    private async Task<Dictionary<VpnServerType, VpnManagerReleaseLatestInfo?>> LoadLatestByTypeAsync(
        IEnumerable<VpnServerType> types,
        CancellationToken cancellationToken)
    {
        var latestByType = new Dictionary<VpnServerType, VpnManagerReleaseLatestInfo?>();
        foreach (var type in types.Distinct())
            latestByType[type] = await releaseLatestService.GetLatestAsync(type, cancellationToken);
        return latestByType;
    }

    private static void Apply(
        VpnServerType serverType,
        string? installedRaw,
        IReadOnlyDictionary<VpnServerType, VpnManagerReleaseLatestInfo?> latestByType,
        out string? installed,
        out string? latest,
        out bool available,
        out string? url)
    {
        installed = string.IsNullOrWhiteSpace(installedRaw) ? null : installedRaw.Trim();
        latestByType.TryGetValue(serverType, out var latestInfo);
        latest = latestInfo?.Version;
        available = VpnManagerReleaseLatestService.IsUpdateAvailable(installed, latest);
        url = latestInfo?.ReleaseUrl;
    }
}
