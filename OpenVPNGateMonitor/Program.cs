using OpenVPNGateMonitor.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureServices(builder.Configuration);
builder.Services.ConfigureGeoLiteServices();
builder.Services.ConfigureAuthServices();
builder.Services.DataBaseServices(builder.Configuration);
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.ConfigureMapster();

builder.Host.ConfigureSerilog(builder.Configuration);

builder.ConfigureWebHost();

builder.Services.AddOpenApi();

var app = builder.Build();

app.ConfigureMiddleware();
app.ConfigurePipeline();

app.Run();