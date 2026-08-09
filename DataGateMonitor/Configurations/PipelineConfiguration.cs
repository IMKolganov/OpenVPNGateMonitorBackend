using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using DataGateMonitor.Hubs;
using Swashbuckle.AspNetCore.Swagger;

namespace DataGateMonitor.Configurations;

public static class PipelineConfiguration
{
    public static void ConfigurePipeline(this WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost,
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.ContainsKey("X-Forwarded-Host"))
            {
                var host = context.Request.Headers["X-Forwarded-Host"].ToString();
                context.Request.Host = new HostString(host);
            }

            if (context.Request.Headers.ContainsKey("X-Forwarded-Proto"))
            {
                var scheme = context.Request.Headers["X-Forwarded-Proto"].ToString();
                context.Request.Scheme = scheme;
            }

            await next();
        });

        app.UseWebSockets();
        app.UseCors("AllowAllOriginsWithCredentials");
        app.UseStaticFiles();

        if (app.Environment.IsDevelopment())
        {
            // app.MapOpenApi();
            app.UseSwagger(o =>
            {
                // IMPORTANT: allow both .json and .yaml on the same route
                o.RouteTemplate = "swagger/{documentName}/swagger.{json|yaml}";
            });
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("/swagger/v1/swagger.json", "Data Gate Monitor API v1");
                o.InjectStylesheet("/swagger-ui/SwaggerDark.css");
                o.RoutePrefix = "swagger";
            });
        }

        // app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        // EF migrations run in EfCoreMigrationHostedService after ApplicationStarted so Swagger/HTTP work while Postgres is down.

        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResultStatusCodes = {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        app.UseStatusCodePagesWithReExecute("/error/{0}");
        app.MapGet("/error/404", () => Results.Problem(statusCode: 404, title: "Page Not Found", 
                detail: "The requested resource was not found."))
            .ExcludeFromDescription();

        app.MapGet("/swaggerjson", async context =>
        {
            var provider = context.RequestServices.GetRequiredService<ISwaggerProvider>();
            var swaggerDoc = provider.GetSwagger("v1");

            context.Response.ContentType = "application/json";

            var stringWriter = new StringWriter();
            var jsonWriter = new OpenApiJsonWriter(stringWriter);
            swaggerDoc.SerializeAsV3(jsonWriter);

            await context.Response.WriteAsync(stringWriter.ToString());
        });

        app.MapHub<GeoLiteHub>("/api/hubs/geoLite");
        app.MapHub<OpenVpnFrontendHub>("/api/hubs/frontend");
        app.MapHub<AdminNotificationHub>("/api/hubs/admin-notify");
        app.MapHub<OpenVpnProxyTrafficFlowHub>("/api/hubs/proxy-traffic-flow");
        app.MapHub<TvLoginHub>("/api/hubs/tv-login");
        app.MapHub<OpenVpnStatusHub>("/api/hubs/status-stream", options =>
        {
            // Default: WebSockets only — publisher sends ~every 700ms; long polling is heavy at scale.
            // Set SignalR:StatusStreamAllowLongPolling=true where WS is blocked (e.g. some dev proxies) — env SignalR__StatusStreamAllowLongPolling.
            var allowLongPolling = app.Configuration.GetValue("SignalR:StatusStreamAllowLongPolling", false);
            options.Transports = allowLongPolling
                ? HttpTransportType.WebSockets | HttpTransportType.LongPolling
                : HttpTransportType.WebSockets;
        });

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";
        var environmentName = app.Environment.EnvironmentName;

        app.MapGet("/",
            (IApplicationDatabaseState databaseState,
                ApplicationRuntimeInfo runtimeInfo,
                IApplicationStartupHistory startupHistory,
                HttpContext context) =>
            {
                var db = databaseState.GetDatabaseStatusLine();
                var accept = context.Request.Headers.Accept.ToString();
                if (accept.Contains("text/plain", StringComparison.OrdinalIgnoreCase)
                    && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    var plain =
                        $"DataGateMonitor Application version: {version}; Environment: {environmentName};\nDatabase: {db}\nStarted: {runtimeInfo.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC\nUptime: {RootPageHtml.FormatUptime(runtimeInfo.Uptime)}";
                    return Results.Text(plain, "text/plain; charset=utf-8", statusCode: 200);
                }

                var html = RootPageHtml.Render(
                    version,
                    environmentName,
                    db,
                    databaseState.GetDatabaseStatusTone(),
                    runtimeInfo,
                    startupHistory.GetRecords());
                return Results.Content(html, "text/html; charset=utf-8", statusCode: 200);
            })
            .ExcludeFromDescription();

        app.Logger.LogInformation($"Application version: {version}; Environment: {environmentName};");

        RsaKeyInitializer.EnsureRsaKeysExist(app.Configuration, app.Logger);
    }
}