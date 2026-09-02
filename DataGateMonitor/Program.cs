using System.Reflection;
using DataGateMonitor.Configurations;
using DataGateMonitor.Services.PiHoleHealth;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureSerilog();

// 🔐
var logger = Log.ForContext("SourceContext", "JwtSecretLoader");
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";

var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
logger.Information("DOTNET_RUNNING_IN_CONTAINER = {IsDocker}", isDocker);
logger.Information($"Application version: {version};");

var jwtSecret = JwtSecretLoaderConfiguration.LoadOrGenerateSecret(builder.Configuration, logger);
builder.Configuration["Jwt:Secret"] = jwtSecret;

#region google secret
builder.Configuration
    .AddJsonFile("secrets/googleauth.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Optional fallback secret (NOT clientId)
var googleAuthSecret = GoogleAuthSecretLoaderConfiguration.LoadSecret(logger);

if (!string.IsNullOrEmpty(googleAuthSecret))
{
    builder.Configuration["GoogleAuth:Secret"] = googleAuthSecret;
}
#endregion

var databaseRuntime = DatabaseRuntimeOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(databaseRuntime);
builder.Services.Configure<HostOptions>(options =>
{
    if (!databaseRuntime.IsConnectionConfigured)
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

if (databaseRuntime.IsConnectionConfigured)
    builder.Services.AddHostedService<EfCoreMigrationHostedService>();

builder.Services.ConfigureServices(builder.Configuration, databaseRuntime);
builder.Services.ConfigureQueryCommand();
builder.Services.ConfigureTelegramServices(builder.Configuration);
builder.Services.ConfigureGeoLiteServices(databaseRuntime);
builder.Services.ConfigureCertExpiryServices(databaseRuntime);
builder.Services.ConfigurePiHoleHealthServices(databaseRuntime);
builder.Services.ConfigureAdminEmailBroadcast();
builder.Services.ConfigureAuthServices(builder.Configuration);
builder.Services.DataBaseServices(builder.Configuration, logger, databaseRuntime);
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.ConfigureMapster();
builder.Services.ConfigureNotificationServices();
builder.Services.ConfigureHealthCheckServices(databaseRuntime);
builder.Services.Configure<CrashIngestOptions>(builder.Configuration.GetSection(CrashIngestOptions.SectionName));
builder.Services.PostConfigure<CrashIngestOptions>(options => options.ApplyDefaults());
builder.Services.Configure<WindowsCrashIngestOptions>(builder.Configuration.GetSection(WindowsCrashIngestOptions.SectionName));
builder.Services.PostConfigure<WindowsCrashIngestOptions>(options => options.ApplyDefaults());

builder.ConfigureWebHost();
builder.ConfigureExternalIpServices();
builder.Services.AddOpenApi();

var app = builder.Build();
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

app.ConfigureMiddleware();
app.ConfigurePipeline();

app.Run();