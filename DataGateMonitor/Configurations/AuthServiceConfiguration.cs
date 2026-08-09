using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using DataGateMonitor.SharedModels.Responses;
using DataGateMonitor.Controllers;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Api.Auth;
using DataGateMonitor.Services.Api.Auth.Handlers;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.TelegramLogin;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.Hubs;
using DataGateMonitor.Services.Api.Auth.Registers;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Api.Auth.Users;
using DataGateMonitor.Services.Api.CurrentUser;
using DataGateMonitor.Services.Api.CurrentUser.Interfaces;

namespace DataGateMonitor.Configurations;

public static class AuthServiceConfiguration
{
    public static void ConfigureAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        services.AddScoped<IUserLoginService, UserLoginService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IUserRegistrationService, UserRegistrationService>();
        services.AddScoped<IGoogleAuthCodeExchangeService, GoogleAuthCodeExchangeService>();
        #region example google env
        // GoogleAuth:ClientId → ENV: GOOGLEAUTH__CLIENTID
        // GoogleAuth:ClientSecret → ENV: GOOGLEAUTH__CLIENTSECRET
        // Auth:PublicWebBaseUrl → ENV: Auth__PublicWebBaseUrl  (compose / .env — public origin for TV QR /tv/link)
        // Auth:TvLoginSessionMinutes → ENV: Auth__TvLoginSessionMinutes
        #endregion
        services.Configure<GoogleAuthSettings>(configuration.GetSection("GoogleAuth"));
        
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        
        services.AddScoped<IUserQuotaPlanService, UserQuotaPlanService>();

        services.AddMemoryCache();
        services.AddSingleton<IAdminIdleSessionTracker, AdminIdleSessionTracker>();
        services.Configure<EmailSenderSettings>(configuration.GetSection("EmailSender"));
        services.AddScoped<SmtpEmailSenderService>();
        services.AddScoped<ResendEmailSenderService>();
        services.AddScoped<IEmailSenderService, ConfigurableEmailSenderService>();
        services.AddScoped<IAdminForgotPasswordService, AdminForgotPasswordService>();
        services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
        services.AddScoped<ITelegramLoginCodeService, TelegramLoginCodeService>();
        services.AddScoped<ITvLoginSessionService, TvLoginSessionService>();
        services.AddScoped<ITvLoginAdminService, TvLoginAdminService>();
        services.AddSingleton<ITvLoginHubNotifier, TvLoginHubNotifier>();
        services.AddScoped<IAdminTotpService, AdminTotpService>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOrOwnServer", policy =>
                policy.Requirements.Add(new AdminOrOwnServerRequirement()));
        });

        services.AddScoped<IVpnServerAccessQueryService, VpnServerAccessQueryService>();
        services.AddScoped<IAuthorizationHandler, AdminOrOwnServerHandler>();

        services.AddScoped<IDeviceService, DeviceService>();

        services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
            })
            .AddNewtonsoftJson();

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "OpenVPN Gate Monitor API",
                    Version = "v1"
                });

                // Short schema ids: namespace tail strips; ApiResponse&lt;T&gt;/PagedResponse&lt;T&gt; → Api.{id(T)} / Paged.{id(T)} (avoids id collision with bare T).
                options.CustomSchemaIds(ToOpenApiComponentSchemaId);

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter the token in the format 'Bearer {your_token}'"
                });

                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            })
            .AddSwaggerGenNewtonsoftSupport();
    }

    /// <summary>
    /// OpenAPI 3.0.3: <c>components.schemas</c> keys must match <c>/^[a-zA-Z0-9.\-_]+$/</c>.
    /// Short ids: strip <c>DataGateMonitor.SharedModels.</c> then <c>DataGateMonitor.</c> from namespace; no <see cref="Type.FullName"/>.
    /// </summary>
    private static string ToOpenApiComponentSchemaId(Type type)
    {
        if (TryUnwrapPayloadWrapperForSchemaId(type, out var payload, out var envelopePrefix))
            return SanitizeOpenApiSchemaKey($"{envelopePrefix}.{ToOpenApiComponentSchemaId(payload)}");

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var defId = ToOpenApiComponentSchemaId(type.GetGenericTypeDefinition());
            var args = string.Join("_", type.GetGenericArguments().Select(ToOpenApiComponentSchemaId));
            return $"{defId}_{args}";
        }

        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick > 0)
            name = name[..tick];

        name = name.Replace('+', '.');

        var ns = type.Namespace ?? string.Empty;
        const string sharedModels = "DataGateMonitor.SharedModels.";
        if (ns.StartsWith(sharedModels, StringComparison.Ordinal))
            ns = ns[sharedModels.Length..];

        const string dataGateMonitorRoot = "DataGateMonitor.";
        if (ns.StartsWith(dataGateMonitorRoot, StringComparison.Ordinal))
            ns = ns[dataGateMonitorRoot.Length..];

        var logical = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return SanitizeOpenApiSchemaKey(logical);
    }

    private static string SanitizeOpenApiSchemaKey(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsAsciiLetter(ch) || char.IsAsciiDigit(ch) || ch is '.' or '-' or '_')
                sb.Append(ch);
            else
                sb.Append('_');
        }

        var id = sb.ToString();
        while (id.Contains("__", StringComparison.Ordinal))
            id = id.Replace("__", "_", StringComparison.Ordinal);
        while (id.Contains("..", StringComparison.Ordinal))
            id = id.Replace("..", ".", StringComparison.Ordinal);
        id = id.Trim('_', '.');
        return id.Length > 0 ? id : "Anonymous";
    }

    /// <summary>
    /// Known envelope types: schema id is short <c>{prefix}.&lt;payload&gt;</c> so it does not collide with bare <c>T</c>.
    /// </summary>
    private static bool TryUnwrapPayloadWrapperForSchemaId(Type type, out Type payload, out string envelopePrefix)
    {
        payload = typeof(void);
        envelopePrefix = string.Empty;
        if (!type.IsGenericType || type.IsGenericTypeDefinition || type.GetGenericArguments().Length != 1)
            return false;

        var def = type.GetGenericTypeDefinition();
        if (def == typeof(ApiResponse<>))
        {
            envelopePrefix = "Api";
            payload = type.GetGenericArguments()[0];
            return true;
        }

        if (def == typeof(PagedResponse<>))
        {
            envelopePrefix = "Paged";
            payload = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }
}