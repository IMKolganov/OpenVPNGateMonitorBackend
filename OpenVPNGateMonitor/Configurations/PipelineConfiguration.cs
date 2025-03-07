using System.Reflection;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

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
            app.UseSwaggerUI();
        }

        // app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        app.UseStatusCodePagesWithReExecute("/error/{0}");
        app.MapGet("/error/404", () => Results.Problem(statusCode: 404, title: "Page Not Found", 
                detail: "The requested resource was not found."))
            .ExcludeFromDescription();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";
        var environmentName = app.Environment.EnvironmentName;
        
        app.MapGet("/",
            (ILogger<IEasyRsaService> logger) => Results.Text(statusCode: 200, 
                content: $"OpenVPNGateMonitor Application version: {version}; Environment: {environmentName};"));

        app.Logger.LogInformation($"Application version: {version}; Environment: {environmentName};");
    }
}