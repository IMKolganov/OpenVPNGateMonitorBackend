using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DataGateMonitor.Services.Api.Auth.Registers;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;

namespace DataGateMonitor.Configurations;

public static class JwtConfiguration
{
    public static void ConfigureJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var key = Encoding.ASCII.GetBytes(configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is missing"));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    //todo: add
                    // ValidateIssuer = true,
                    // ValidIssuer = "OpenVPNGateBackend",
                    // ValidateAudience = true,
                    // ValidAudience = "OpenVPNGateFrontend",

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                        if (string.IsNullOrEmpty(token))
                        {
                            token = context.Request.Query["access_token"];
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (!JwtBearerEventHandlers.IsExpectedClientTokenFailure(context.Exception))
                            return Task.CompletedTask;

                        // Fail without attaching the exception so JwtBearer/ Serilog do not emit stack traces (Wazuh).
                        context.Fail("Access token expired.");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        await JwtBearerEventHandlers.RejectRevokedAppClientAsync(context);
                    },
                };
            });
        services.AddSingleton<IMicroserviceTokenService, MicroserviceTokenService>();
    }
}
