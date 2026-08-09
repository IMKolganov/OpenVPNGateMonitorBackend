using System.Data.Common;
using DataGateMonitor.Services.Performance;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Data.Interceptors;

public sealed class SlowDbCommandInterceptor(
    IPerformanceSampleStore sampleStore,
    IOptions<PerformanceMonitoringOptions> options,
    ILogger<SlowDbCommandInterceptor> logger) : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, succeeded: true, exception: null);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Record(command, eventData.Duration, succeeded: false, exception: eventData.Exception);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, succeeded: false, exception: eventData.Exception);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    /// <summary>Visible for unit tests.</summary>
    internal void Record(
        DbCommand command,
        TimeSpan duration,
        bool succeeded,
        Exception? exception)
    {
        try
        {
            var durationMs = (long)duration.TotalMilliseconds;
            var threshold = Math.Max(0, options.Value.DbSlowMs);
            if (succeeded && durationMs < threshold)
                return;

            var sqlMax = Math.Clamp(options.Value.SqlMaxLength, 64, 16_000);
            var sql = Truncate(command.CommandText, sqlMax);

            // Fire-and-forget; interceptor must not block the EF pipeline on Redis.
            _ = sampleStore.AppendDbAsync(
                new PerformanceDbSample
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    RequestId = PerformanceRequestContext.RequestId,
                    DurationMs = durationMs,
                    CommandType = command.CommandType.ToString(),
                    Sql = sql,
                    Succeeded = succeeded,
                    ExceptionMessage = Truncate(exception?.Message, 500)
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to record DB performance sample.");
        }
    }

    internal static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max ? value : value[..max] + "…";
    }
}
