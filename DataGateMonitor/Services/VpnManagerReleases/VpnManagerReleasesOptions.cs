using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.VpnManagerReleases;

public sealed class VpnManagerReleasesOptions
{
    public const string SectionName = "VpnManagerReleases";

    public int CacheMinutes { get; set; } = 30;

    public string UserAgent { get; set; } = "DataGateMonitor";

    public VpnManagerReleaseRepoOptions OpenVpn { get; set; } = new()
    {
        Owner = "IMKolganov",
        Repo = "DataGateCertManager",
    };

    public VpnManagerReleaseRepoOptions Xray { get; set; } = new()
    {
        Owner = "IMKolganov",
        Repo = "DataGateXRayManager",
    };

    public VpnManagerReleaseRepoOptions? Resolve(VpnServerType serverType) =>
        serverType switch
        {
            VpnServerType.OpenVpn => OpenVpn,
            VpnServerType.Xray => Xray,
            _ => null,
        };
}

public sealed class VpnManagerReleaseRepoOptions
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
}
