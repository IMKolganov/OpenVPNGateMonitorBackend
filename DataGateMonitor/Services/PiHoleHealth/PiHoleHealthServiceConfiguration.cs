using DataGateMonitor.Configurations;

namespace DataGateMonitor.Services.PiHoleHealth;

public static class PiHoleHealthServiceConfiguration
{
    public static void ConfigurePiHoleHealthServices(this IServiceCollection services, DatabaseRuntimeOptions databaseRuntime)
    {
        services.AddSingleton<PiHoleHealthNotificationTracker>();
        services.AddScoped<IPiHoleHealthNotificationService, PiHoleHealthNotificationService>();
        services.AddScoped<IPiHoleHealthCheckRunner, PiHoleHealthCheckRunner>();

        if (databaseRuntime.IsConnectionConfigured)
        {
            services.AddHostedService<PiHoleHealthBackgroundService>();
            // Xray Pi-hole collection runs on the Xray node (like OpenVPN), not in the dashboard.
        }
    }
}
