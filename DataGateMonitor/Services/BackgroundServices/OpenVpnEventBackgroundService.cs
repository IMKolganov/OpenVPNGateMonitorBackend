using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.DataGateOpenVpnManager.Events;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.BackgroundServices;

public sealed class OpenVpnEventBackgroundService(
    ILogger<OpenVpnEventBackgroundService> logger,
    IOpenVpnEventClientFactory eventClientFactory,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    private static int _instanceCount = 0;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var newInstanceCount = Interlocked.Increment(ref _instanceCount);
        if (newInstanceCount > 1)
        {
            logger.LogCritical("Multiple instances detected! Total instances: {NewInstanceCount}", newInstanceCount);
            throw new InvalidOperationException("Only one instance of OpenVpnEventBackgroundService is allowed.");
        }

        logger.LogInformation("OpenVpnEventBackgroundService started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = scopeFactory.CreateScope();
                var openVpnOverviewQuery = scope.ServiceProvider.GetRequiredService<IVpnServerQueryService>();
                var servers = await openVpnOverviewQuery.GetAll(ct: cancellationToken);
                servers = servers
                    .Where(x => !x.IsDisable && x.ServerType == VpnServerType.OpenVpn)
                    .ToList();

                var activeIds = servers.Select(x => x.Id).ToHashSet();
                foreach (var cached in eventClientFactory.GetAllClients())
                {
                    var cachedId = cached.GetStatus().ConnectionStatus.ServerId;
                    if (!activeIds.Contains(cachedId))
                    {
                        logger.LogInformation(
                            "Pruning OpenVPN event client for deleted/disabled server {ServerId}", cachedId);
                        eventClientFactory.Remove(cachedId);
                    }
                }

                foreach (var server in servers)
                {
                    try
                    {
                        var client = eventClientFactory.Create(server);
                        await client.StartListeningAsync(cancellationToken);
                        logger.LogDebug("Event listener active for server {ServerId} ({ApiUrl})", server.Id, server.ApiUrl);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Client StopAsync after delete/update — continue with remaining servers.
                        logger.LogInformation(
                            "OpenVPN event listener stopped for server {ServerId} (removed from cache)", server.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to start listening to server {ServerId}", server.Id);
                    }
                }

                // Re-sync periodically so updated server URLs (after Update) get a new client and reconnect
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
            logger.LogInformation("OpenVpnEventBackgroundService stopping...");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unhandled error in OpenVpnEventBackgroundService");
            throw;
        }
    }
}