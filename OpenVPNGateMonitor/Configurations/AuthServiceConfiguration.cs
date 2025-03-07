using OpenVPNGateMonitor.Services.Api.Auth;

namespace OpenVPNGateMonitor.Configurations;

public static class AuthServiceConfiguration
{
    public static void ConfigureAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        
        services.AddAuthorization();
        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }
}
