using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.DataBase.Repositories;
using OpenVPNGateMonitor.DataBase.Repositories.Interfaces;
using OpenVPNGateMonitor.DataBase.Repositories.Queries;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models.Helpers;
using Microsoft.EntityFrameworkCore;

namespace OpenVPNGateMonitor.Configurations;

public static class DataBaseConfigurations
{
    public static void DataBaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
                               ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Database connection string is missing.");

        var dbSettings = new DataBaseSettings
        {
            DefaultSchema = Environment.GetEnvironmentVariable("DB_DEFAULT_SCHEMA") 
                            ?? configuration["DataBaseSettings:DefaultSchema"],

            MigrationTable = Environment.GetEnvironmentVariable("DB_MIGRATION_TABLE") 
                             ?? configuration["DataBaseSettings:MigrationTable"]
        };

        // Scoped ApplicationDbContext
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    dbSettings.MigrationTable ?? "__EFMigrationsHistory",
                    dbSettings.DefaultSchema ?? "public"
                )
            );
        }, ServiceLifetime.Scoped);

        // Scoped DbContextFactory
        services.AddDbContextFactory<ApplicationDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
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