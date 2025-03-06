﻿using OpenVPNGateMonitor.Models.Helpers;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace OpenVPNGateMonitor.Configurations;

public static class SerilogConfiguration
{
    public static void ConfigureSerilog(this IHostBuilder host, IConfiguration configuration)
    {
        var elasticsearchSettings = configuration.GetSection("Elasticsearch").Get<ElasticsearchSettings>();
        if (elasticsearchSettings == null) throw new NullReferenceException(nameof(elasticsearchSettings));
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchSettings.Uri))
            {
                IndexFormat = elasticsearchSettings.IndexFormat,
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                NumberOfShards = 1,
                NumberOfReplicas = 0,
                ModifyConnectionSettings = conn => conn
                    .ServerCertificateValidationCallback((sender, cert, chain, errors) => true)
                    .BasicAuthentication(elasticsearchSettings.Username, elasticsearchSettings.Password),
                FailureCallback = (logEvent, exception) =>
                {
                    Console.WriteLine($"Unable to submit event: {logEvent.RenderMessage()}");
                    if (exception != null)
                        Console.WriteLine($"Exception: {exception.Message}");
                },
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog

            })
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .CreateLogger();
;

        host.UseSerilog();
    }
}