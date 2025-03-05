using MaxMind.GeoIP2;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite.Untils;

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
    
    /// <summary>
    /// Returns the current instance of the database reader. If it's not loaded, loads it first.
    /// </summary>
    public async Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        _lock.EnterReadLock();
        try
        {
            if (_currentDb != null)
                return _currentDb;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // If _currentDb is null, load the database
        await LoadDatabaseAsync(cancellationToken);
        
        _lock.EnterReadLock();
        try
        {
            return _currentDb ?? throw new InvalidOperationException("Database is not loaded.");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    /// <summary>
    /// Loads the database from the configured file path.
    /// </summary>
    public async Task LoadDatabaseAsync(CancellationToken cancellationToken)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_currentDb != null)
                return;

            _lock.EnterWriteLock();
            try
            {
                _dbPath = await GeoLiteLoadConfigs.GetStringParamFromSettings(GeoIpDbPath, _serviceProvider, cancellationToken);

                await CloseDatabaseAsync(cancellationToken);

                if (!File.Exists(_dbPath))
                {
                    _logger.LogWarning("Database file not found: {DbPath}", _dbPath);
                    return;
                }

                _currentDb = new DatabaseReader(_dbPath);
                _logger.LogInformation("Loaded GeoLite2 database from {DbPath}", _dbPath);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load GeoLite2 database.");
            throw;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Closes the current database reader, releasing the lock on the file.
    /// </summary>
    public async Task CloseDatabaseAsync(CancellationToken cancellationToken)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_currentDb != null)
            {
                _logger.LogInformation("Closing GeoLite2 database...");
                _currentDb.Dispose();
                _currentDb = null;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reloads the database by closing the old instance and loading a new one.
    /// </summary>
    public async Task ReloadDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reloading GeoLite2 database...");
        await LoadDatabaseAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the current database file path.
    /// </summary>
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
