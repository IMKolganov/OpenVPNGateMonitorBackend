using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.EasyRsaServices;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;
using OpenVPNGateMonitor.Services.Helpers;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;
using OpenVPNGateMonitor.Services.Others;

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
        
        services.AddScoped<IOpenVpnServerService, OpenVpnServerService>();

        services.AddSingleton<CommandQueueManager>();
        services.AddSingleton<ICommandQueueManager>(provider => provider.GetRequiredService<CommandQueueManager>());

        services.AddSingleton<EasyRsaService>();
        services.AddSingleton<IEasyRsaService>(provider => provider.GetRequiredService<EasyRsaService>());

        services.AddSingleton<EasyRsaParseDbService>();
        services.AddSingleton<IEasyRsaParseDbService>(provider => provider.GetRequiredService<EasyRsaParseDbService>());

        services.AddSingleton<EasyRsaExecCommandService>();
        services.AddSingleton<IEasyRsaExecCommandService>(provider => provider.GetRequiredService<EasyRsaExecCommandService>());

        
        services.AddScoped<IOpenVpnTelnetService, OpenVpnTelnetService>();
        
        services.AddScoped<IVpnDataService, VpnDataService>();
        services.AddScoped<ICertVpnService, CertVpnService>();
        services.AddScoped<IOvpnFileService, OvpnFileService>();

        services.AddSingleton<OpenVpnServerStatusManager>();
        services.AddSingleton<OpenVpnServerProcessorFactory>();

        services.AddSingleton<OpenVpnBackgroundService>();
        services.AddSingleton<IOpenVpnBackgroundService>(provider => provider.GetRequiredService<OpenVpnBackgroundService>());
        services.AddHostedService(provider => provider.GetRequiredService<OpenVpnBackgroundService>());
        
        services.AddScoped<IOpenVpnServerOvpnFileConfigService, OpenVpnServerOvpnFileConfigService>();
        services.AddScoped<ISettingsService, SettingsService>();
        
        services.AddScoped<ExternalIpAddressService>();
    }
}
