using DataGateMonitor.Models.Helpers;
using DataGateMonitor.SharedModels.Notifications.Requests;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Others.Notifications.ServerOpenVpnApiClient;

public class ServerOpenVpnNotificationService(INotificationService notifications) : IServerOpenVpnNotificationService
{
    private static readonly string[] InfoChannels = ["web", "telegram"];
    private static readonly string[] ErrorChannels = ["web", "telegram"];
    private const string Source = "server-openvpn-api";

    public Task NotifyBecameAvailable(int serverId, string? serverName, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerBecameAvailable, "server.became-available", "VPN server became available",
            Detail(serverId, serverName), serverId, NotificationSeverity.Info, InfoChannels, ct);

    public Task NotifyAdded(int serverId, string? serverName, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerAdded, "server.added", "VPN server added",
            Detail(serverId, serverName), serverId, NotificationSeverity.Info, InfoChannels, ct);

    public Task NotifyUpdated(int serverId, string? serverName, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerUpdated, "server.updated", "VPN server updated",
            Detail(serverId, serverName), serverId, NotificationSeverity.Info, InfoChannels, ct);

    public Task NotifyDeleted(int serverId, string? serverName, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerDeleted, "server.deleted", "VPN server deleted",
            Detail(serverId, serverName), serverId, NotificationSeverity.Warning, InfoChannels, ct);

    public Task NotifyBecameUnavailableDueToError(int serverId, string? serverName, string? errorMessage, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerBecameUnavailable, "server.became-unavailable", "VPN server became unavailable due to error",
            Detail(serverId, serverName) + (string.IsNullOrEmpty(errorMessage) ? "" : $"; Error={errorMessage}"),
            serverId, NotificationSeverity.Error, ErrorChannels, ct);

    public Task NotifySyncError(int serverId, string? serverName, string? errorMessage, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerSyncError, "server.sync-error", "Synchronization error with VPN server",
            Detail(serverId, serverName) + (string.IsNullOrEmpty(errorMessage) ? "" : $"; Error={errorMessage}"),
            serverId, NotificationSeverity.Error, ErrorChannels, ct);

    public Task NotifyNoResponseFromServer(int serverId, string? serverName, CancellationToken ct)
        => Notify(ApplicationNotificationKind.OpenVpnServerNoResponse, "server.no-response", "No response from VPN server",
            Detail(serverId, serverName), serverId, NotificationSeverity.Warning, ErrorChannels, ct);

    public Task NotifyDiscovered(int discoveryId, string? suggestedName, string apiUrl, CancellationToken ct)
    {
        var namePart = string.IsNullOrEmpty(suggestedName) ? "" : $"; Name={suggestedName}";
        var message = $"DiscoveryId={discoveryId}{namePart}; ApiUrl={apiUrl}";
        return Notify(
            ApplicationNotificationKind.OpenVpnServerDiscovered,
            NotificationTypes.ServerDiscovered,
            "VPN server discovered",
            message,
            serverId: null,
            NotificationSeverity.Info,
            InfoChannels,
            ct);
    }

    private static string Detail(int serverId, string? serverName)
        => $"ServerId={serverId}" + (string.IsNullOrEmpty(serverName) ? "" : $"; Name={serverName}");

    private Task Notify(ApplicationNotificationKind preferenceKind, string type, string title, string message, int? serverId, NotificationSeverity severity,
        string[] channels, CancellationToken ct)
        => notifications.NotifyAdmins(new NotifyAdminsRequest
        {
            Type = type,
            Title = title,
            Message = message,
            Severity = severity,
            Source = Source,
            ServerId = serverId,
            PreferenceKind = preferenceKind
        }, channels, ct);
}
