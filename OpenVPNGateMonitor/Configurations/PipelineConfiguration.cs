using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Writers;
using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;
using Swashbuckle.AspNetCore.Swagger;

namespace OpenVPNGateMonitor.Configurations;

public static class PipelineConfiguration
{
    public static void ConfigurePipeline(this WebApplication app)
    {
        app.UseWebSockets();
        app.UseCors("AllowAllOrigins"); 
        if (app.Environment.IsDevelopment())
        {
            // app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenVPN Gate Monitor API v1");
            });
        }

        // app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            try
            {
                var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();

                if (pendingMigrations.Any())
                {
                    app.Logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                        pendingMigrations.Count, string.Join(", ", pendingMigrations));
                    dbContext.Database.Migrate();
                    app.Logger.LogInformation("Migrations applied successfully.");
                }
                else
                {
                    app.Logger.LogInformation("Database is up-to-date. No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, $"An error occurred while applying migrations: {ex.Message}");
                throw; // optionally rethrow if you want the app to crash
            }
        }
        
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

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";
        var environmentName = app.Environment.EnvironmentName;
        
        app.MapGet("/",
            (ILogger<IEasyRsaService> logger) => Results.Text(statusCode: 200, 
                content: $"OpenVPNGateMonitor Application version: {version}; Environment: {environmentName};"));

        app.Logger.LogInformation($"Application version: {version}; Environment: {environmentName};");
    }
}