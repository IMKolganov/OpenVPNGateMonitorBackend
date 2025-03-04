using MaxMind.GeoIP2;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoLiteDatabaseFactory
{
    Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken);
    Task LoadDatabaseAsync(CancellationToken cancellationToken);
    Task ReloadDatabaseAsync(CancellationToken cancellationToken);
    Task<string> GetDatabasePath();
}