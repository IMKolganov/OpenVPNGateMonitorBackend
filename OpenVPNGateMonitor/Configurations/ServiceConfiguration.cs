using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.BackgroundServices;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
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
            
            options.AddPolicy("AllowAllOrigins",
                policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });
        
        services.AddScoped<IOpenVpnManagementService, OpenVpnManagementService>();
        services.AddScoped<IOpenVpnClientService, OpenVpnClientService>();
        services.AddScoped<IOpenVpnStateService, OpenVpnStateService>();
        services.AddScoped<IOpenVpnSummaryStatService, OpenVpnSummaryStatService>();
        services.AddScoped<IOpenVpnVersionService, OpenVpnVersionService>();
        
        services.AddScoped<IGeoIpService, GeoIpService>();
        
        services.AddScoped<IOpenVpnServerService, OpenVpnServerService>();

        
        services.AddSingleton<IEasyRsaService, EasyRsaService>();

        services.AddScoped<IVpnDataService, VpnDataService>();
        services.AddScoped<ICertVpnService, CertVpnService>();

        services.AddSingleton<OpenVpnBackgroundService>();
        services.AddSingleton<IOpenVpnBackgroundService>(sp => sp.GetRequiredService<OpenVpnBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<OpenVpnBackgroundService>());

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
    }
}
