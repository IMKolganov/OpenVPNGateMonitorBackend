using Mapster;
using MapsterMapper;
using OpenVPNGateMonitor.SharedModels.Applications.Mappings;
using OpenVPNGateMonitor.SharedModels.Auth.Mappings;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Mappings;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Mappings;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Mappings;

namespace OpenVPNGateMonitor.Configurations;

public static class MapsterConfiguration
{
    public static void ConfigureMapster(this IServiceCollection services)
    {
        var config = new TypeAdapterConfig();

        config.Scan(typeof(AuthMapping).Assembly);
        config.Scan(typeof(ApplicationMapping).Assembly);
        config.Scan(typeof(OvpnFileMapping).Assembly);
        config.Scan(typeof(VpnServerCertificateMapping).Assembly);
        config.Scan(typeof(OvpnFileConfigMapping).Assembly);
        
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}