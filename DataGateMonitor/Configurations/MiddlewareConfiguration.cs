using DataGateMonitor.Middlewares;

namespace DataGateMonitor.Configurations;

public static class MiddlewareConfiguration
{
    public static void ConfigureMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<RequestTimingMiddleware>();
    }
}