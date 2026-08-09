using StackExchange.Redis;

namespace DataGateMonitor.Services.Cache;

/// <summary>
/// Optional Redis access for cache counters. Returns null when Redis is not configured or unavailable.
/// </summary>
public interface IRedisDatabaseProvider
{
    Task<IDatabase?> GetDatabaseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a StackExchange.Redis multiplexer from a connection string.
/// </summary>
public interface IRedisMultiplexerConnector
{
    Task<IConnectionMultiplexer> ConnectAsync(string connectionString, CancellationToken cancellationToken = default);
}

public sealed class StackExchangeRedisMultiplexerConnector : IRedisMultiplexerConnector
{
    public async Task<IConnectionMultiplexer> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
        => await ConnectionMultiplexer.ConnectAsync(connectionString);
}

/// <summary>
/// Lazily connects using Redis connection string from configuration.
/// A failed connect is remembered so callers fall back to DB without retry storms.
/// </summary>
public sealed class ConfigurationRedisDatabaseProvider(
    IConfiguration configuration,
    ILogger<ConfigurationRedisDatabaseProvider> logger,
    IRedisMultiplexerConnector multiplexerConnector) : IRedisDatabaseProvider
{
    private readonly string? _connectionString =
        configuration.GetConnectionString("Redis")
        ?? configuration["Redis:ConnectionString"]
        ?? configuration["REDIS_CONNECTION_STRING"];
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private IConnectionMultiplexer? _multiplexer;
    private bool _connectAttempted;

    public async Task<IDatabase?> GetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        if (_multiplexer is { IsConnected: true })
            return _multiplexer.GetDatabase();

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_multiplexer is { IsConnected: true })
                return _multiplexer.GetDatabase();

            if (_connectAttempted)
                return null;

            _connectAttempted = true;
            try
            {
                _multiplexer = await multiplexerConnector.ConnectAsync(_connectionString, cancellationToken);
                logger.LogInformation("Redis connected for connected-clients counters.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis unavailable; connected-clients counters will fall back to DB.");
                _multiplexer = null;
            }

            return _multiplexer?.GetDatabase();
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
