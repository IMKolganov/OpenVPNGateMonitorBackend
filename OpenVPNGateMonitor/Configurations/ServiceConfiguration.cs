using OpenVPNGateMonitor.Services;
using OpenVPNGateMonitor.Services.BotServices;
using OpenVPNGateMonitor.Services.Interfaces;
using OpenVPNGateMonitor.Services.UntilsServices;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Configurations;

public static class ServiceConfiguration
{
    public static void ConfigureServices(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigin",
                policy =>
                {
                    policy.WithOrigins("http://localhost:3000") // front
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });
        
        services.AddScoped<IOpenVpnParserService, OpenVpnParserService>();
        services.AddSingleton<IEasyRsaService, EasyRsaService>();

        services.AddHostedService<OpenVpnBackgroundService>();
        

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
    }
}
