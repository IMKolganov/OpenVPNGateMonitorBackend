using MaxMind.GeoIP2;
using OpenVPNGateMonitor.Services.GeoLite.Helpers;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoLiteDatabaseFactory
{
    private readonly ILogger<GeoLiteDatabaseFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DatabaseReader? _currentDb;
    private string? _dbPath;
    private const string GeoIpDbPath = "GeoIp_Db_Path";
    private Task? _loadingTask;

    public bool IsDatabaseLoaded => _currentDb != null;

    public GeoLiteDatabaseFactory(ILogger<GeoLiteDatabaseFactory> logger, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_currentDb != null)
            return _currentDb;

        await EnsureDatabaseLoadedAsync(cancellationToken);
        
        if (_currentDb == null)
            throw new InvalidOperationException("Database is not loaded.");

        return _currentDb;
    }

    private async Task EnsureDatabaseLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loadingTask != null)
        {
            await _loadingTask.WaitAsync(cancellationToken);
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsDatabaseLoaded)
                return;

            _loadingTask = LoadDatabaseInternalAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }

        await _loadingTask.WaitAsync(cancellationToken);
    }

    private async Task LoadDatabaseInternalAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
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
            _loadingTask = null;
            _semaphore.Release();
        }
    }
    
    public async Task DeleteDatabaseAsync()
    {
        await _semaphore.WaitAsync();
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
            _semaphore.Release();
        }
    }

    public async Task ReloadDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reloading GeoLite2 database...");
        await DeleteDatabaseAsync();
        await EnsureDatabaseLoadedAsync(cancellationToken);
    }

    public async Task<string> GetDatabasePathAsync()
    {
        if (_dbPath == null)
            throw new InvalidOperationException("Database is not loaded.");

        return await Task.FromResult(_dbPath);
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
