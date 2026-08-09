using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Cache;

namespace DataGateMonitor.Services.Helpers;

public interface IVpnServerClientPresenceService
{
    /// <summary>
    /// Marks all connected sessions for a VPN server as disconnected and zeroes the Redis counter.
    /// Use when the node is unreachable, disabled, deleted, or otherwise no longer observed.
    /// </summary>
    Task MarkAllDisconnectedAsync(int vpnServerId, CancellationToken cancellationToken);
}

public sealed class VpnServerClientPresenceService(
    ICommandService<VpnServerClient, int> vpnServerClientCommandService,
    IConnectedClientsCounterStore connectedClientsCounterStore,
    ILogger<VpnServerClientPresenceService> logger) : IVpnServerClientPresenceService
{
    public async Task MarkAllDisconnectedAsync(int vpnServerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await vpnServerClientCommandService.UpdateWhere(
            x => x.VpnServerId == vpnServerId && x.IsConnected,
            s => s
                .SetProperty(c => c.IsConnected, false)
                .SetProperty(c => c.DisconnectedAt, now)
                .SetProperty(c => c.LastUpdate, now),
            cancellationToken);

        await connectedClientsCounterStore.SetAsync(vpnServerId, 0, cancellationToken);

        logger.LogInformation(
            "VpnServerId: {Id}. Marked {Count} connected client session(s) disconnected; Redis counter set to 0.",
            vpnServerId, updated);
    }
}
