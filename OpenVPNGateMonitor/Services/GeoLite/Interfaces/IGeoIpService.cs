using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoIpService
{
    Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken);
    Task DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken);
    Task<string> GetDataBasePath(CancellationToken cancellationToken);
    OpenVpnGeoInfo? GetGeoInfo(string ip);
}