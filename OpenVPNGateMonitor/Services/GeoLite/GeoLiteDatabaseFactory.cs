using MaxMind.GeoIP2;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite.Untils;
using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoLiteDatabaseFactory : IGeoLiteDatabaseFactory
{
    private readonly ILogger<GeoLiteDatabaseFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ReaderWriterLockSlim _lock = new();
    private DatabaseReader? _currentDb;
    private string? _dbPath;
    private const string GeoIpDbPath = "GeoIp_Db_Path";

    public GeoLiteDatabaseFactory(ILogger<GeoLiteDatabaseFactory> logger, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        _lock.EnterReadLock();
        try
        {
            if (_currentDb == null)
            {
                _lock.ExitReadLock();
                await LoadDatabaseAsync(cancellationToken);
                _lock.EnterReadLock();
            }

            return _currentDb ?? throw new InvalidOperationException("Database is not loaded.");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    public async Task LoadDatabaseAsync(CancellationToken cancellationToken)
    {
        _lock.EnterWriteLock();
        try
        {
            _dbPath = await GeoLiteLoadConfigs.GetStringParamFromSettings(GeoIpDbPath, _serviceProvider,
                cancellationToken);

            if (_currentDb != null)
            {
                _currentDb.Dispose();
                _logger.LogInformation("Disposed the old database instance.");
            }

            if (!File.Exists(_dbPath))
            {
                _logger.LogWarning("Database file not found: {DbPath}", _dbPath);
                return;
            }

            _currentDb = new DatabaseReader(_dbPath);
            _logger.LogInformation("Loaded GeoLite2 database from {DbPath}", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load GeoLite2 database.");
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public async Task ReloadDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reloading GeoLite2 database...");
        await LoadDatabaseAsync(cancellationToken);
    }
    
    public Task<string> GetDatabasePath()
    {
        _lock.EnterReadLock();
        try
        {
            if (_dbPath == null)
                throw new InvalidOperationException("Database is not loaded.");

            return Task.FromResult(_dbPath);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
