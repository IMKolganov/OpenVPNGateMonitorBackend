using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenVPNGateMonitor.Models.Helpers;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenVPNGateMonitor.Configurations
{
    public static class SerilogConfiguration
    {
        public static void ConfigureSerilog(this IHostBuilder host, IConfiguration configuration)
        {
            var elasticsearchSettings = configuration.GetSection("Elasticsearch").Get<ElasticsearchSettings>();
            if (elasticsearchSettings == null) throw new NullReferenceException(nameof(elasticsearchSettings));

            var nodes = new List<Uri> { new Uri(elasticsearchSettings.Uri) };

            try
            {
                var settings = new ElasticsearchClientSettings(new Uri(elasticsearchSettings.Uri))
                    .CertificateFingerprint("IGNORE")
                    .ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);
                
                if (!string.IsNullOrEmpty(elasticsearchSettings.Username) && !string.IsNullOrEmpty(elasticsearchSettings.Password))
                {
                    settings = settings.Authentication(new BasicAuthentication(
                        elasticsearchSettings.Username,
                        elasticsearchSettings.Password
                    ));
                }

                var client = new ElasticsearchClient(settings);

                // Проверяем соединение с Elasticsearch
                var pingResponse = client.Ping();
                if (!pingResponse.IsValidResponse)
                {
                    Console.WriteLine($"❌ Elasticsearch connection failed: {pingResponse.DebugInformation}");
                    return;
                }
                Console.WriteLine("✅ Elasticsearch connection successful!");

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.Elasticsearch(
                        nodes,
                        opts =>
                        {
                            opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("logs", "dotnet");
                            opts.IlmPolicy = "logs";
                            opts.MinimumLevel = LogEventLevel.Information;
                        },
                        transportConfig =>
                        {
                            if (!string.IsNullOrEmpty(elasticsearchSettings.Username) &&
                                !string.IsNullOrEmpty(elasticsearchSettings.Password))
                            {
                                transportConfig.Authentication(new BasicAuthentication(
                                    elasticsearchSettings.Username,
                                    elasticsearchSettings.Password));
                            }
                        })
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .CreateLogger();

                host.UseSerilog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Serilog initialization failed: {ex.Message}");
            }
        }
    }
}
