using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.DataBase.Repositories;
using OpenVPNGateMonitor.DataBase.Repositories.Interfaces;
using OpenVPNGateMonitor.DataBase.Repositories.Queries;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models.Helpers;
using Microsoft.EntityFrameworkCore;
using ILogger = Serilog.ILogger;

namespace OpenVPNGateMonitor.Configurations;

public static class DataBaseConfigurations
{
    public static void DataBaseServices(this IServiceCollection services, IConfiguration configuration,
        ILogger logger)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING_DATAGATE") 
                               ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Database connection string is missing. " +
                "Attempted to read from environment variable 'DB_CONNECTION_STRING_DATAGATE' or " +
                "configuration key 'ConnectionStrings:DefaultConnection'.");
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            logger.Information("Using PostgreSQL Database. Host: {Host}, Port: {Port}, Database: {Database}", 
                builder.Host, builder.Port, builder.Database);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to parse connection string for logging.");
        }

        var dbSettings = new DataBaseSettings
        {
            DefaultSchema = Environment.GetEnvironmentVariable("DB_DEFAULT_SCHEMA") 
                            ?? configuration["DataBaseSettings:DefaultSchema"],

            MigrationTable = Environment.GetEnvironmentVariable("DB_MIGRATION_TABLE") 
                             ?? configuration["DataBaseSettings:MigrationTable"]
        };

        // Scoped ApplicationDbContext
        services.AddDbContext<ApplicationDbContext>((options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    dbSettings.MigrationTable ?? "__EFMigrationsHistory",
                    dbSettings.DefaultSchema ?? "public"
                )
            );
        }, ServiceLifetime.Scoped);

        // Scoped DbContextFactory
        services.AddDbContextFactory<ApplicationDbContext>((options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    dbSettings.MigrationTable ?? "__EFMigrationsHistory",
                    dbSettings.DefaultSchema ?? "public"
                )
            );
        }, ServiceLifetime.Scoped);
        
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IQueryFactory, QueryFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>(); 
    }
}