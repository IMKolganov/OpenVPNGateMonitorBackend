using OpenVPNGateMonitor.Services.GeoLite;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;

namespace OpenVPNGateMonitor.Configurations;

public static class GeoLiteServiceConfiguration
{
    public static void ConfigureGeoLiteServices(this IServiceCollection services)
    {
        services.AddSingleton<GeoLiteDatabaseFactory>();

        services.AddScoped<IGeoLiteQueryService, GeoLiteQueryService>();
        services.AddSingleton<IGeoLiteUpdaterService, GeoLiteUpdaterService>();
        services.AddHttpClient<IGeoLiteUpdaterService, GeoLiteUpdaterService>();
    }
}
