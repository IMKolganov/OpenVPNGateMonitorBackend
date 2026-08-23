namespace DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;

public interface IServerOpenVpnNotificationService
{
    /// <summary>Server became available.</summary>
    Task NotifyBecameAvailable(int serverId, string? serverName, CancellationToken ct);

    /// <summary>Server was added.</summary>
    Task NotifyAdded(int serverId, string? serverName, CancellationToken ct);

    /// <summary>Server was updated.</summary>
    Task NotifyUpdated(int serverId, string? serverName, CancellationToken ct);

    /// <summary>Server was deleted.</summary>
    Task NotifyDeleted(int serverId, string? serverName, CancellationToken ct);

    /// <summary>Server became unavailable due to an error.</summary>
    Task NotifyBecameUnavailableDueToError(int serverId, string? serverName, string? errorMessage, CancellationToken ct);

    /// <summary>Synchronization error with server.</summary>
    Task NotifySyncError(int serverId, string? serverName, string? errorMessage, CancellationToken ct);

    /// <summary>No response from server.</summary>
    Task NotifyNoResponseFromServer(int serverId, string? serverName, CancellationToken ct);

    /// <summary>VPN node announced itself and awaits admin approve/deny. <paramref name="discoveryId"/> is the pending discovery row; ServerId is not set.</summary>
    Task NotifyDiscovered(int discoveryId, string? suggestedName, string apiUrl, CancellationToken ct);
}
