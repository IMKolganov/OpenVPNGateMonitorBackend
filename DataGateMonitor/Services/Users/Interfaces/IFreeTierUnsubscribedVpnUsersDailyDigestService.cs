using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;

namespace DataGateMonitor.Services.Users.Interfaces;

public interface IFreeTierUnsubscribedVpnUsersDailyDigestService
{
    /// <summary>
    /// When enabled, sends bot admins a digest of Free/Default users currently on VPN without
    /// channel subscription. At most once per UTC day. Never throws.
    /// </summary>
    Task TrySendDailyDigestAsync(CancellationToken ct = default);

    /// <summary>
    /// Builds the current digest text (online Free/Default users without channel subscription).
    /// </summary>
    Task<string> BuildDigestTextAsync(CancellationToken ct = default);

    /// <summary>
    /// Live digest text plus structured candidates (for admin bot inline buttons).
    /// </summary>
    Task<FreeTierUnsubscribedVpnDigestResponse> BuildDigestAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks the once-per-UTC-day daily digest as already satisfied (e.g. after a live admin fetch).
    /// </summary>
    void MarkDailyDigestSatisfiedForToday();
}
