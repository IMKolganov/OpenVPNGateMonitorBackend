using Mapster;
using MapsterMapper;
using OpenVPNGateMonitor.SharedModels.Applications.Mappings;
using OpenVPNGateMonitor.SharedModels.Auth.Mappings;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Mappings;

namespace OpenVPNGateMonitor.Configurations;

public static class MapsterConfiguration
{
    public static void ConfigureMapster(this IServiceCollection services)
    {
        var config = new TypeAdapterConfig();

        config.Scan(typeof(AuthMapping).Assembly);
        config.Scan(typeof(ApplicationMapping).Assembly);
        config.Scan(typeof(OvpnFileMapping).Assembly);
        
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}