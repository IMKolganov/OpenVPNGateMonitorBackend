using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Services.BackgroundServices;

public class OpenVpnServerProcessor(
    ILogger<OpenVpnServerProcessor> logger,
    IServiceProvider serviceProvider) : IVpnServerWorkProcessor
{
    public async Task ProcessServerAsync(VpnServer openVpnServer, CancellationToken ct)
    {
        logger.LogInformation(
            "OpenVpnServerProcessor: VpnServerId: {Id}. Name: {Name}. Processing: {Url}",
            openVpnServer.Id, openVpnServer.ServerName, openVpnServer.ApiUrl);

        using var scope = serviceProvider.CreateScope();
        var openVpnServerService = scope.ServiceProvider.GetRequiredService<IVpnServerService>();
        var serverCmd = scope.ServiceProvider.GetRequiredService<ICommandService<VpnServer, int>>();
        var presence = scope.ServiceProvider.GetRequiredService<IVpnServerClientPresenceService>();

        try
        {
            logger.LogInformation("Saving status log for {Url}", openVpnServer.ApiUrl);
            await openVpnServerService.SaveVpnServerStatusLogAsync(openVpnServer, ct);

            logger.LogInformation("Saving connected clients for {Url}", openVpnServer.ApiUrl);
            await openVpnServerService.SaveConnectedClientsAsync(openVpnServer, ct);

            logger.LogInformation("Fetching and saving conflog for {Url}", openVpnServer.ApiUrl);
            await scope.ServiceProvider.GetRequiredService<IVpnServerConflogService>().FetchAndSaveIfChangedByServerIdAsync(openVpnServer.Id, ct);

            // Set IsOnline = true (server-side update, no entity tracking)
            var now = DateTimeOffset.UtcNow;
            await serverCmd.UpdateWhere(
                s => s.Id == openVpnServer.Id,
                u => u.SetProperty(x => x.IsOnline, true)
                    .SetProperty(x => x.LastUpdate, now),
                ct);

            logger.LogInformation(
                "OpenVpnServerProcessor: VpnServerId: {Id}. Name: {Name}. Finished processing: {Url}",
                openVpnServer.Id, openVpnServer.ServerName, openVpnServer.ApiUrl);
        }
        catch (Exception ex)
        {
            // Mark server offline and clear hanging "online" sessions — we can no longer observe the node.
            var now = DateTimeOffset.UtcNow;
            await serverCmd.UpdateWhere(
                s => s.Id == openVpnServer.Id,
                u => u.SetProperty(x => x.IsOnline, false)
                    .SetProperty(x => x.LastUpdate, now),
                ct);
            await presence.MarkAllDisconnectedAsync(openVpnServer.Id, ct);

            logger.LogError(ex,
                "OpenVpnServerProcessor error. VpnServerId: {Id}. Name: {Name}. Url: {Url}",
                openVpnServer.Id, openVpnServer.ServerName, openVpnServer.ApiUrl);

            throw;
        }
    }
}
