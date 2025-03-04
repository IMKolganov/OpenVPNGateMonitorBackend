namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoLiteUpdaterService
{
    Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken);
    Task DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken);
}