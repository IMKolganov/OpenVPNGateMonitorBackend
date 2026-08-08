using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.XrayNode;

namespace DataGateMonitor.Services.BackgroundServices;

/// <summary>
/// Xray node polling: GET active clients from the agent, sync to <see cref="VpnServerClient"/>, update <see cref="VpnServer.IsOnline"/>.
/// </summary>
public sealed class XrayServerProcessor(
    ILogger<XrayServerProcessor> logger,
    IServiceProvider serviceProvider) : IVpnServerWorkProcessor
{
    private const int MaxPollErrorLength = 2000;

    public async Task ProcessServerAsync(VpnServer server, CancellationToken ct)
    {
        logger.LogInformation(
            "XrayServerProcessor: VpnServerId: {Id}. Name: {Name}. Base Url: {Url}",
            server.Id, server.ServerName, server.ApiUrl);

        using var scope = serviceProvider.CreateScope();
        var xrayApi = scope.ServiceProvider.GetRequiredService<IXrayNodeApiClient>();
        var sync = scope.ServiceProvider.GetRequiredService<IXrayVpnClientSyncService>();
        var serverCmd = scope.ServiceProvider.GetRequiredService<ICommandService<VpnServer, int>>();
        var presence = scope.ServiceProvider.GetRequiredService<IVpnServerClientPresenceService>();

        var now = DateTimeOffset.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(server.ApiUrl))
                throw new InvalidOperationException("ApiUrl is required for Xray server polling.");

            var clientsPayload = await xrayApi.GetActiveClientsAsync(server.ApiUrl.TrimEnd('/'), ct);
            if (clientsPayload is null)
            {
                throw new HttpRequestException(
                    "Xray node did not return a successful clients response (see logs for status).");
            }

            await sync.SyncConnectedClientsAsync(server, clientsPayload.Clients, ct);

            var statusLog = scope.ServiceProvider.GetRequiredService<IXrayVpnServerStatusLogService>();
            await statusLog.TryAppendOrUpdateAsync(server, clientsPayload, ct);

            var pollNote = TruncatePollMessage(clientsPayload.PollError);
            await serverCmd.UpdateWhere(
                s => s.Id == server.Id,
                u => u.SetProperty(x => x.IsOnline, true)
                    .SetProperty(x => x.LastUpdate, now)
                    .SetProperty(x => x.XrayClientsPolledAt, now)
                    .SetProperty(x => x.XrayClientsPollError, pollNote),
                ct);

            logger.LogInformation(
                "XrayServerProcessor: VpnServerId: {Id}. Name: {Name}. Synced {Count} client session(s).",
                server.Id, server.ServerName, clientsPayload.Clients.Count);
        }
        catch (Exception ex)
        {
            var err = TruncatePollMessage(ex.Message);
            await serverCmd.UpdateWhere(
                s => s.Id == server.Id,
                u => u.SetProperty(x => x.IsOnline, false)
                    .SetProperty(x => x.LastUpdate, now)
                    .SetProperty(x => x.XrayClientsPolledAt, now)
                    .SetProperty(x => x.XrayClientsPollError, err),
                ct);
            await presence.MarkAllDisconnectedAsync(server.Id, ct);

            logger.LogError(ex,
                "XrayServerProcessor error. VpnServerId: {Id}. Name: {Name}. Url: {Url}",
                server.Id, server.ServerName, server.ApiUrl);

            throw;
        }
    }

    private static string? TruncatePollMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return null;
        return message.Length <= MaxPollErrorLength
            ? message
            : message[..MaxPollErrorLength];
    }
}
