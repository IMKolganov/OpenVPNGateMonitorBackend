using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DataGateMonitor.Configurations;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Blocks until <see cref="DbContext.Database.CanConnect"/> succeeds.
    /// Use when PostgreSQL starts after the API (e.g. docker-compose race). Set <c>DB_WAIT_FOR_STARTUP_SECONDS</c> to fail after N seconds (0 = unlimited).
    /// </summary>
    public static void WaitUntilDatabaseCanConnect<TDbContext>(
        IConfiguration configuration,
        ILogger logger,
        TDbContext dbContext)
        where TDbContext : DbContext
    {
        var maxWaitSeconds = configuration.GetValue("DB_WAIT_FOR_STARTUP_SECONDS", 0);
        var sw = Stopwatch.StartNew();
        var delayMs = 500;
        const int maxDelayMs = 15_000;
        var announced = false;
        var lastSummaryLog = TimeSpan.Zero;

        while (true)
        {
            try
            {
                if (dbContext.Database.CanConnect())
                {
                    if (announced)
                        logger.LogInformation("PostgreSQL became reachable after {Elapsed}.", sw.Elapsed);
                    return;
                }

                if (!announced)
                {
                    logger.LogWarning(
                        "PostgreSQL did not accept a connection yet (CanConnect returned false); retrying. " +
                        "Set DB_WAIT_FOR_STARTUP_SECONDS to cap wait (0 = unlimited).");
                    announced = true;
                }
            }
            catch (Exception ex)
            {
                if (!IsTransientDatabaseStartupFailure(ex))
                    throw;

                if (!announced)
                {
                    logger.LogWarning(ex,
                        "PostgreSQL is not reachable yet; retrying until it accepts connections. " +
                        "Set DB_WAIT_FOR_STARTUP_SECONDS to cap wait (0 = unlimited).");
                    announced = true;
                }
                else if (sw.Elapsed - lastSummaryLog >= TimeSpan.FromSeconds(15))
                {
                    logger.LogInformation("Still waiting for PostgreSQL ({Elapsed})...", sw.Elapsed);
                    lastSummaryLog = sw.Elapsed;
                }
            }

            if (maxWaitSeconds > 0 && sw.Elapsed.TotalSeconds >= maxWaitSeconds)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL did not become reachable within {maxWaitSeconds}s (DB_WAIT_FOR_STARTUP_SECONDS).");
            }

            Thread.Sleep(delayMs);
            delayMs = Math.Min(maxDelayMs, delayMs * 2);
        }
    }

    public static void ApplyMigrationsWithDetailedLogging<TDbContext>(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger logger)
        where TDbContext : DbContext
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        WaitUntilDatabaseCanConnect(configuration, logger, dbContext);

        try
        {
            var pending = dbContext.Database.GetPendingMigrations().ToList();
            if (!pending.Any())
            {
                logger.LogInformation("Database is up-to-date. No pending migrations.");
                return;
            }

            logger.LogInformation("Applying {Count} pending migrations: {List}",
                pending.Count, string.Join(", ", pending));

            // Apply all pending migrations in one Migrate() call. Do NOT call Migrate(target) per migration:
            // when history has gaps (a later migration applied but an earlier one missing), targeting the
            // earlier id makes EF revert every applied migration with a higher id first — e.g. Down() on
            // VpnServers_ServerNameUniqueActiveOnly recreates a full unique index and fails on prod data.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                var migrator = dbContext.Database.GetService<IMigrator>();
                var sw = Stopwatch.StartNew();

                try
                {
                    migrator.Migrate();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    LogMigrationFailure(logger, pending.FirstOrDefault() ?? "(unknown)", ex);
                    throw new InvalidOperationException(
                        $"Migration failed while applying pending migrations (first pending: {pending[0]})",
                        ex);
                }

                sw.Stop();
                logger.LogInformation(
                    "Applied {Count} pending migration(s) in {Elapsed} ms: {List}",
                    pending.Count, sw.ElapsedMilliseconds, string.Join(", ", pending));
            });

            logger.LogInformation("All pending migrations applied successfully.");
        }
        catch (Exception ex)
        {
            var pg = FindPostgresException(ex);
            if (pg is not null)
            {
                logger.LogError(ex,
                    "An error occurred while applying migrations. PostgresSQL SqlState={SqlState}, Message={MessageText}",
                    pg.SqlState, pg.MessageText);
            }
            else
            {
                logger.LogError(ex, "An error occurred while applying migrations: {Message}", ex.Message);
            }

            throw;
        }
    }

    /// <summary>True when the server is probably not up yet (retry). False for auth / bad DB name — fail fast.</summary>
    private static bool IsTransientDatabaseStartupFailure(Exception ex)
    {
        if (FindPostgresException(ex) is { SqlState: var sql })
        {
            if (sql is "28P01" or "28000")
                return false;
            if (sql is "3D000")
                return false;
        }

        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            if (cur is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.TimedOut
                    or SocketError.HostDown or SocketError.NetworkUnreachable })
                return true;
        }

        if (ex is NpgsqlException or TimeoutException or IOException)
            return true;

        return false;
    }

    private static PostgresException? FindPostgresException(Exception ex)
    {
        var cur = ex;
        while (cur != null)
        {
            if (cur is PostgresException pge)
                return pge;

            cur = cur.InnerException;
        }

        return null;
    }

    private static void LogMigrationFailure(ILogger logger, string migration, Exception ex)
    {
        var pg = FindPostgresException(ex);
        if (pg is not null)
        {
            logger.LogError(ex,
                "Migration FAILED: {Migration}. PostgresSQL SqlState={SqlState}, Message={MessageText}, Severity={Severity}, Routine={Routine}",
                migration, pg.SqlState, pg.MessageText, pg.Severity, pg.Routine);
        }
        else
        {
            logger.LogError(ex, "Migration FAILED: {Migration}. {Message}", migration, ex.Message);
        }
    }
}
