using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoLiteQueryService
{
    Task<OpenVpnGeoInfo?> GetGeoInfoAsync(string ip, CancellationToken cancellationToken);
    Task<string> GetDatabasePathAsync();
}