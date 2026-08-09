using System.Collections.Concurrent;
using DataGateMonitor.Serialization;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DataGateMonitor.Services.Performance;

public sealed class PerformanceSampleStore : IPerformanceSampleStore
{
    private const string RedisHttpKey = "performance:http";
    private const string RedisDbKey = "performance:db";
    private const int MaxAllowedEntries = 2000;
    private const int DefaultMaxEntries = 500;

    private readonly ConcurrentQueue<PerformanceHttpSample> _httpMemory = new();
    private readonly ConcurrentQueue<PerformanceDbSample> _dbMemory = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ILogger<PerformanceSampleStore> _logger;
    private readonly string? _connectionString;
    private readonly int _maxEntries;
    private IConnectionMultiplexer? _multiplexer;
    private bool _connectAttempted;

    public PerformanceSampleStore(
        IConfiguration configuration,
        IOptions<PerformanceMonitoringOptions> options,
        ILogger<PerformanceSampleStore> logger)
    {
        _logger = logger;
        _connectionString =
            configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? configuration["REDIS_CONNECTION_STRING"];
        _maxEntries = ResolveMaxEntries(options.Value.MaxEntries, configuration);
    }

    public Task AppendHttpAsync(PerformanceHttpSample sample, CancellationToken ct = default) =>
        AppendAsync(RedisHttpKey, _httpMemory, NormalizeHttp(sample), ct);

    public Task AppendDbAsync(PerformanceDbSample sample, CancellationToken ct = default) =>
        AppendAsync(RedisDbKey, _dbMemory, NormalizeDb(sample), ct);

    public Task<IReadOnlyList<PerformanceHttpSample>> GetHttpLatestAsync(int limit, CancellationToken ct = default) =>
        GetLatestAsync(RedisHttpKey, _httpMemory, limit, ct);

    public Task<IReadOnlyList<PerformanceDbSample>> GetDbLatestAsync(int limit, CancellationToken ct = default) =>
        GetLatestAsync(RedisDbKey, _dbMemory, limit, ct);

    public Task ClearHttpAsync(CancellationToken ct = default) => ClearAsync(RedisHttpKey, _httpMemory, ct);

    public Task ClearDbAsync(CancellationToken ct = default) => ClearAsync(RedisDbKey, _dbMemory, ct);

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await ClearHttpAsync(ct);
        await ClearDbAsync(ct);
    }

    private async Task AppendAsync<T>(
        string redisKey,
        ConcurrentQueue<T> memory,
        T sample,
        CancellationToken ct)
    {
        EnqueueMemory(memory, sample);

        var db = await GetDatabaseOrNullAsync(ct);
        if (db is null)
            return;

        try
        {
            var redisValue = ProjectJson.Serialize(sample);
            await db.ListLeftPushAsync(redisKey, redisValue);
            await db.ListTrimAsync(redisKey, 0, _maxEntries - 1);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis write failed for performance samples ({Key})", redisKey);
        }
    }

    private async Task<IReadOnlyList<T>> GetLatestAsync<T>(
        string redisKey,
        ConcurrentQueue<T> memory,
        int limit,
        CancellationToken ct)
    {
        var normalizedLimit = NormalizeLimit(limit);
        var db = await GetDatabaseOrNullAsync(ct);
        if (db is not null)
        {
            try
            {
                var values = await db.ListRangeAsync(redisKey, 0, normalizedLimit - 1);
                if (values.Length > 0)
                {
                    var list = new List<T>(values.Length);
                    foreach (var value in values)
                    {
                        if (!value.HasValue)
                            continue;

                        var raw = value.ToString();
                        if (string.IsNullOrWhiteSpace(raw))
                            continue;

                        var parsed = ProjectJson.Deserialize<T>(raw);
                        if (parsed is null)
                            continue;

                        list.Add(parsed);
                    }

                    if (list.Count > 0)
                        return list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Redis read failed for performance samples ({Key})", redisKey);
            }
        }

        return GetMemorySnapshot(memory, normalizedLimit);
    }

    private async Task ClearAsync<T>(string redisKey, ConcurrentQueue<T> memory, CancellationToken ct)
    {
        while (memory.TryDequeue(out _))
        {
        }

        var db = await GetDatabaseOrNullAsync(ct);
        if (db is null)
            return;

        try
        {
            await db.KeyDeleteAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis delete failed for performance samples ({Key})", redisKey);
        }
    }

    private void EnqueueMemory<T>(ConcurrentQueue<T> memory, T sample)
    {
        memory.Enqueue(sample);
        while (memory.Count > _maxEntries && memory.TryDequeue(out _))
        {
        }
    }

    private static IReadOnlyList<T> GetMemorySnapshot<T>(ConcurrentQueue<T> memory, int limit)
    {
        var array = memory.ToArray();
        if (array.Length == 0)
            return [];

        var take = Math.Min(limit, array.Length);
        var start = array.Length - take;
        var list = new List<T>(take);
        for (var i = array.Length - 1; i >= start; i--)
            list.Add(array[i]);

        return list;
    }

    private static PerformanceHttpSample NormalizeHttp(PerformanceHttpSample sample) =>
        new()
        {
            TimestampUtc = sample.TimestampUtc == default ? DateTimeOffset.UtcNow : sample.TimestampUtc,
            RequestId = sample.RequestId ?? string.Empty,
            Method = sample.Method ?? string.Empty,
            Path = sample.Path ?? string.Empty,
            StatusCode = sample.StatusCode,
            DurationMs = sample.DurationMs,
            UserName = sample.UserName,
            TraceId = sample.TraceId
        };

    private static PerformanceDbSample NormalizeDb(PerformanceDbSample sample) =>
        new()
        {
            TimestampUtc = sample.TimestampUtc == default ? DateTimeOffset.UtcNow : sample.TimestampUtc,
            RequestId = sample.RequestId,
            DurationMs = sample.DurationMs,
            CommandType = sample.CommandType ?? string.Empty,
            Sql = sample.Sql ?? string.Empty,
            Succeeded = sample.Succeeded,
            ExceptionMessage = sample.ExceptionMessage
        };

    private async Task<IDatabase?> GetDatabaseOrNullAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        if (_multiplexer is { IsConnected: true })
            return _multiplexer.GetDatabase();

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_multiplexer is { IsConnected: true })
                return _multiplexer.GetDatabase();

            if (_connectAttempted)
                return null;

            _connectAttempted = true;
            try
            {
                _multiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString);
                _logger.LogInformation("Redis connected for performance samples.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis unavailable; performance samples will remain in memory.");
                _multiplexer = null;
            }

            return _multiplexer?.GetDatabase();
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private static int ResolveMaxEntries(int fromOptions, IConfiguration configuration)
    {
        var configured =
            configuration.GetValue<int?>("PerformanceMonitoring:MaxEntries")
            ?? configuration.GetValue<int?>("PERFORMANCE_MONITORING_MAX_ENTRIES")
            ?? (fromOptions > 0 ? fromOptions : DefaultMaxEntries);

        if (configured <= 0)
            return DefaultMaxEntries;

        return Math.Min(configured, MaxAllowedEntries);
    }

    private int NormalizeLimit(int limit)
    {
        if (limit <= 0)
            return _maxEntries;

        return Math.Min(limit, _maxEntries);
    }
}
