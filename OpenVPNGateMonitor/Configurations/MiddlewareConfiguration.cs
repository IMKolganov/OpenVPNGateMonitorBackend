using System.Reflection;
using OpenVPNGateMonitor.Middlewares;

namespace OpenVPNGateMonitor.Configurations;

public static class MiddlewareConfiguration
{
    public static void ConfigureMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}