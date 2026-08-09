using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;

namespace DataGateMonitor.Services.Users.Interfaces;

public interface IFreeTierEnforcementOverviewService
{
    /// <summary>
    /// Free/Default users who are not compliant right now (channel subscription missing and no active
    /// grace) — i.e. everyone the enforcement job would kill on its next run, whether or not they are
    /// connected.
    /// </summary>
    Task<GetFreeTierEnforcementCandidatesResponse> GetCandidatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Free/Default users who are currently online and not subscribed to the required channel.
    /// Includes grace-period users (still unsubscribed). Used by the admin digest.
    /// </summary>
    Task<GetFreeTierEnforcementCandidatesResponse> GetUnsubscribedConnectedAsync(
        CancellationToken ct = default);

    Task<GetFreeTierDisconnectLogResponse> GetDisconnectLogAsync(
        GetFreeTierDisconnectLogRequest request, CancellationToken ct = default);
}
