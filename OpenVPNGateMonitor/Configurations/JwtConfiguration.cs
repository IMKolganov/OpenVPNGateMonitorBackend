using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace OpenVPNGateMonitor.Configurations;

public static class JwtConfiguration
{
    public static void ConfigureJwt(this IServiceCollection services)
    {
        var key = Encoding.ASCII.GetBytes("SuperSecretKeyForSigningJwtTokens123!");//todo: move to config

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
    }
}