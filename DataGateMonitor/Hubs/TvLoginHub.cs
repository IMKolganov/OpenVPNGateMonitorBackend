using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

namespace DataGateMonitor.Hubs;

/// <summary>
/// Anonymous hub for Android TV (and similar) to watch a device-linking session in real time.
/// Join with <see cref="WatchSession"/>; listen for <c>TvLoginSessionStatusChanged</c>.
/// When status becomes <c>approved</c>, call GET /api/auth/tv/session/{sessionId} once to receive tokens.
/// </summary>
[AllowAnonymous]
public sealed class TvLoginHub(ITvLoginSessionQueryService sessionQuery) : Hub
{
    public const string StatusChangedEvent = "TvLoginSessionStatusChanged";
    public const string HubPath = "/api/hubs/tv-login";

    public static string GroupName(Guid sessionId) => $"tv-login:{sessionId:D}";

    /// <summary>Subscribe to status updates for a TV login session created via POST /api/auth/tv/session.</summary>
    public async Task WatchSession(Guid sessionId)
    {
        var session = await sessionQuery.GetById(sessionId, Context.ConnectionAborted);
        if (session is null)
            throw new HubException("TV login session not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));

        // Immediate snapshot so the TV UI can render without an extra HTTP round-trip.
        var status = MapStatus(session);
        await Clients.Caller.SendAsync(
            StatusChangedEvent,
            new TvLoginSessionStatusEvent
            {
                SessionId = session.Id,
                Status = status,
                ExpiresAt = session.ExpiresAt,
            },
            Context.ConnectionAborted);
    }

    public Task UnwatchSession(Guid sessionId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));

    private static string MapStatus(TvLoginSession session)
    {
        if (session.Status is TvLoginSessionStatus.Pending or TvLoginSessionStatus.Viewed
            && session.ExpiresAt <= DateTimeOffset.UtcNow)
            return "expired";

        return session.Status switch
        {
            TvLoginSessionStatus.Pending => "pending",
            TvLoginSessionStatus.Viewed => "viewed",
            TvLoginSessionStatus.Approved => "approved",
            TvLoginSessionStatus.Denied => "denied",
            TvLoginSessionStatus.Expired => "expired",
            TvLoginSessionStatus.Consumed => "consumed",
            _ => "expired",
        };
    }
}
