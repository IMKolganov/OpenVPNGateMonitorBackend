using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoIpService
{
    Task<string> GetDatabaseVersionAsync(string downloadUrl);
    Task DownloadAndUpdateDatabaseAsync(string downloadUrl, string dbPath);
    string GetDataBasePath();
    OpenVpnGeoInfo? GetGeoInfo(string ip);
}