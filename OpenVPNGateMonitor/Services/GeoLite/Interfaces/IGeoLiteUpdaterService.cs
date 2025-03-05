using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoLiteUpdaterService
{
    Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken);
    Task<GeoLiteUpdateResponse>  DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken);
}