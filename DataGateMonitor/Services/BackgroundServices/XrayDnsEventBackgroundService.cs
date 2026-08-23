using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.DataGateXRayManager.Events;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.BackgroundServices;

public sealed class XrayDnsEventBackgroundService(
    ILogger<XrayDnsEventBackgroundService> logger,
    IXrayDnsEventClientFactory eventClientFactory,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("XrayDnsEventBackgroundService started");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = scopeFactory.CreateScope();
                var servers = await scope.ServiceProvider
                    .GetRequiredService<IVpnServerQueryService>()
                    .GetAll(ct: cancellationToken);

                foreach (var server in servers.Where(s => !s.IsDisable && s.ServerType == VpnServerType.Xray))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(server.ApiUrl))
                            continue;
                        var client = eventClientFactory.Create(server);
                        await client.StartListeningAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to start Xray DNS event listener for server {ServerId}", server.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("XrayDnsEventBackgroundService stopping...");
        }
    }
}
