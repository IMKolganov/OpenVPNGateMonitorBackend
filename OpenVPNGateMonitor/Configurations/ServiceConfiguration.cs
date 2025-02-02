using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.BackgroundServices;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.UntilsServices;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Configurations;

public static class ServiceConfiguration
{
    public static void ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var frontendSettings = configuration.GetSection("Frontend").Get<FrontendSettings>();
        services.AddCors(options =>
        {   
            
            options.AddPolicy("AllowSpecificOrigin",
                policy =>
                {
                    policy.WithOrigins(frontendSettings?.FrontUrl ?? 
                                       throw new InvalidOperationException("FrontUrl not found")) 
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });
        
        // services.AddScoped<IOpenVpnParserService, OpenVpnParserService>();
        services.AddScoped<IVpnManagementService, OpenVpnManagementService>();
        services.AddSingleton<IEasyRsaService, EasyRsaService>();

        services.AddHostedService<OpenVpnBackgroundService>();
        

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
    }
}
