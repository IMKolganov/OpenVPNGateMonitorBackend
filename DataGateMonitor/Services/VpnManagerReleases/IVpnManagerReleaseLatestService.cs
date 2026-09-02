using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.VpnManagerReleases;

public sealed class VpnManagerReleaseLatestInfo
{
    public required VpnServerType ServerType { get; init; }
    public required string Version { get; init; }
    public string? ReleaseUrl { get; init; }
}

public interface IVpnManagerReleaseLatestService
{
    Task<VpnManagerReleaseLatestInfo?> GetLatestAsync(VpnServerType serverType, CancellationToken cancellationToken);
}
