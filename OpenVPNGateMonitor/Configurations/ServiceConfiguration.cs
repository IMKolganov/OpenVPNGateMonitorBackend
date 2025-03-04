using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.Api.Auth;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;
using OpenVPNGateMonitor.Services.Others;
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
        
        services.AddScoped<IOpenVpnClientService, OpenVpnClientService>();
        services.AddScoped<IOpenVpnStateService, OpenVpnStateService>();
        services.AddScoped<IOpenVpnSummaryStatService, OpenVpnSummaryStatService>();
        services.AddScoped<IOpenVpnVersionService, OpenVpnVersionService>();
        
        services.AddSingleton<IGeoIpService, GeoIpIpService>();
        
        services.AddScoped<IOpenVpnServerService, OpenVpnServerService>();

        services.AddSingleton<ICommandQueueManager, CommandQueueManager>();

        services.AddSingleton<IEasyRsaService, EasyRsaService>();
        services.AddSingleton<IEasyRsaParseDbService, EasyRsaParseDbService>();
        services.AddSingleton<IEasyRsaExecCommandService, EasyRsaExecCommandService>();
        
        services.AddScoped<IOpenVpnTelnetService, OpenVpnTelnetService>();
        

        services.AddScoped<IVpnDataService, VpnDataService>();
        services.AddScoped<ICertVpnService, CertVpnService>();
        services.AddScoped<IOvpnFileService, OvpnFileService>();

        services.AddSingleton<OpenVpnServerStatusManager>();
        services.AddSingleton<OpenVpnServerProcessorFactory>();

        services.AddHostedService<OpenVpnBackgroundService>();
        services.AddSingleton<IOpenVpnBackgroundService, OpenVpnBackgroundService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        
        services.AddScoped<IOpenVpnServerOvpnFileConfigService, OpenVpnServerOvpnFileConfigService>();
        services.AddScoped<ISettingsService, SettingsService>();

        services.AddAuthorization();
        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
    }
}
