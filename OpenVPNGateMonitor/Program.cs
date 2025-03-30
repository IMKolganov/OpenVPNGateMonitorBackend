using OpenVPNGateMonitor.Configurations;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureSerilog(builder.Configuration);

// 🔐
var logger = Log.ForContext("SourceContext", "JwtSecretLoader");

var jwtSecret = JwtSecretLoaderConfiguration.LoadOrGenerateSecret(logger);
builder.Configuration["Jwt:Secret"] = jwtSecret;

builder.Services.ConfigureServices(builder.Configuration);
builder.Services.ConfigureGeoLiteServices();
builder.Services.ConfigureAuthServices();
builder.Services.DataBaseServices(builder.Configuration);
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.ConfigureMapster();

builder.ConfigureWebHost();
builder.ConfigureExternalIpServices();
builder.Services.AddOpenApi();

var app = builder.Build();

app.ConfigureMiddleware();
app.ConfigurePipeline();

app.Run();