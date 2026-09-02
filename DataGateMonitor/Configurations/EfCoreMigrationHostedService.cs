using DataGateMonitor.DataBase.Contexts;
using Microsoft.Extensions.Hosting;

namespace DataGateMonitor.Configurations;

/// <summary>
/// Runs EF wait-for-Postgres and migrations after the host has fully started so HTTP (e.g. Swagger) is available while the database is still down.
/// </summary>
public sealed class EfCoreMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ApplicationDatabaseState databaseState,
    ILogger<EfCoreMigrationHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Run before HTTP/background workers: ApplicationStarted was too late and caused
        // queries against columns/tables that migrations had not created yet.
        databaseState.SetWaitingOrMigrating();
        try
        {
            DatabaseMigrationExtensions.ApplyMigrationsWithDetailedLogging<ApplicationDbContext>(
                scopeFactory, configuration, logger);
            databaseState.SetReady();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migrations failed; API stays up (Swagger without DB). Fix PostgreSQL and restart.");
            databaseState.SetFailed(ex.GetBaseException().Message);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
