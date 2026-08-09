namespace DataGateMonitor.Services.Users.Interfaces;

public interface IFreeTierUnsubscribedUserReminderService
{
    /// <summary>
    /// Sends a Telegram DM asking the user to subscribe to the required channel, when the setting
    /// is enabled and the per-user cooldown has elapsed. Never throws.
    /// </summary>
    Task TryRemindAsync(long telegramId, string context, CancellationToken ct = default);
}
