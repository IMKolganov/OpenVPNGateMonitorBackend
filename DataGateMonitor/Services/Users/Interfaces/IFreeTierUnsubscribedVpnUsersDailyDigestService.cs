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
    /// Used by the daily job and by on-demand admin requests. Always evaluates live candidates.
    /// </summary>
    Task<string> BuildDigestTextAsync(CancellationToken ct = default);
}
