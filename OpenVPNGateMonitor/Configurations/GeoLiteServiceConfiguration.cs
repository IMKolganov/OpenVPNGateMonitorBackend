using OpenVPNGateMonitor.Services.GeoLite;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;

namespace OpenVPNGateMonitor.Configurations;

public static class GeoLiteServiceConfiguration
{
    public static void ConfigureGeoLiteServices(this IServiceCollection services)
    {
        services.AddSingleton<GeoLiteDatabaseFactory>();

        services.AddScoped<IGeoLiteQueryService, GeoLiteQueryService>();

        services.AddSingleton<GeoLiteUpdaterService>();
        services.AddSingleton<IGeoLiteUpdaterService>(provider => provider.GetRequiredService<GeoLiteUpdaterService>());

        services.AddHttpClient<IGeoLiteUpdaterService, GeoLiteUpdaterService>();
    }
}
