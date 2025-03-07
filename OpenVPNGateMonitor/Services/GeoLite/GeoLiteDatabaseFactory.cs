using MaxMind.GeoIP2;
using OpenVPNGateMonitor.Services.GeoLite.Helpers;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoLiteDatabaseFactory
{
    private readonly ILogger<GeoLiteDatabaseFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private DatabaseReader? _currentDb;
    private string? _dbPath;
    private const string GeoIpDbPath = "GeoIp_Db_Path";
    private Task? _loadingTask;

    /// <summary>
    /// Indicates whether the database is loaded.
    /// </summary>
    public bool IsDatabaseLoaded => _currentDb != null;

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
        _lock.TryEnterReadLock(10000);
        try
        {
            if (_currentDb != null)
                return _currentDb;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        await EnsureDatabaseLoadedAsync(cancellationToken);

        _lock.TryEnterReadLock(10000);
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
    /// Ensures that multiple concurrent requests wait until the DB is fully loaded.
    /// </summary>
    private async Task EnsureDatabaseLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loadingTask != null)
        {
            try
            {
                await _loadingTask.WaitAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                _loadingTask = null;
                return;
            }
            catch (Exception ex)
            {
                _loadingTask = null;
                throw new InvalidOperationException("Database loading failed.", ex);
            }
        }

        _lock.TryEnterUpgradeableReadLock(10000);
        try
        {
            if (IsDatabaseLoaded)
                return;

            _loadingTask = Task.Run(() => LoadDatabaseInternalAsync(cancellationToken), cancellationToken);
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }

        try
        {
            await _loadingTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loadingTask = null;
            return;
        }
        catch (Exception ex)
        {
            _loadingTask = null;
            throw new InvalidOperationException("Database loading failed.", ex);
        }
    }

    private async Task LoadDatabaseInternalAsync(CancellationToken cancellationToken)
    {
        _lock.TryEnterWriteLock(10000);
        try
        {
            if (IsDatabaseLoaded)
                return;

            _logger.LogInformation("Closing and reloading GeoLite2 database...");
            CloseDatabase();

            _dbPath = await GeoLiteLoadConfigs.GetStringParamFromSettings(GeoIpDbPath, _serviceProvider, cancellationToken);
            if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
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
            _loadingTask = null;
        }
    }
    
    public void DeleteDatabase()
    {
        _lock.TryEnterWriteLock(10000);
        try
        {
            if (IsDatabaseLoaded)
            {
                CloseDatabase();
            }

            if (_dbPath != null && File.Exists(_dbPath))
            {
                _logger.LogInformation("Deleting GeoLite2 database file: {DbPath}", _dbPath);
                File.Delete(_dbPath);
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

        _lock.TryEnterWriteLock(10000);
        try
        {
            CloseDatabase();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        await EnsureDatabaseLoadedAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the current database file path.
    /// </summary>
    public Task<string> GetDatabasePath()
    {
        _lock.TryEnterReadLock(10000);
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
    
    private void CloseDatabase()
    {
        if (_currentDb == null)
            return;

        _logger.LogInformation("Closing GeoLite2 database...");
        _currentDb.Dispose();
        _currentDb = null;
    }
}
