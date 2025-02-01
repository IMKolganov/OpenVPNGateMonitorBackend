using OpenVPNGateMonitor.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureServices();
builder.Services.DataBaseServices(builder.Configuration);

builder.Host.ConfigureSerilog(builder.Configuration);

builder.ConfigureWebHost();

builder.Services.AddOpenApi();

var app = builder.Build();

app.ConfigureMiddleware();
app.ConfigurePipeline();

app.Run();