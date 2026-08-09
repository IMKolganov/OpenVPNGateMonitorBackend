using Microsoft.AspNetCore.SignalR;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

namespace DataGateMonitor.Hubs;

public interface ITvLoginHubNotifier
{
    Task NotifyStatusAsync(
        Guid sessionId,
        string status,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);
}

public sealed class TvLoginHubNotifier(
    IHubContext<TvLoginHub> hubContext,
    ILogger<TvLoginHubNotifier> logger) : ITvLoginHubNotifier
{
    public async Task NotifyStatusAsync(
        Guid sessionId,
        string status,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        try
        {
            await hubContext.Clients
                .Group(TvLoginHub.GroupName(sessionId))
                .SendAsync(
                    TvLoginHub.StatusChangedEvent,
                    new TvLoginSessionStatusEvent
                    {
                        SessionId = sessionId,
                        Status = status,
                        ExpiresAt = expiresAt,
                    },
                    ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push TV login status {Status} for session {SessionId}", status, sessionId);
        }
    }
}
