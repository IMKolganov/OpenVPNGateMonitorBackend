namespace DataGateMonitor.Services.TelegramBot.Interfaces;

public interface ITelegramAdminAlertService
{
    /// <summary>
    /// Notifies bot admins that a Telegram account was linked to a dashboard identity (Google/local).
    /// Includes avatars when available. Never throws.
    /// </summary>
    Task NotifyAccountsLinkedAsync(TelegramAccountsLinkedAlert alert, CancellationToken ct = default);
}

public sealed class TelegramAccountsLinkedAlert
{
    public long TelegramId { get; init; }
    public string? TelegramUsername { get; init; }
    public string? TelegramDisplayName { get; init; }
    public string LinkedProviderLabel { get; init; } = "Google";
    public string? LinkedDisplayName { get; init; }
    public string? LinkedEmail { get; init; }
    public string? LinkedExternalId { get; init; }
    public string? LinkedAvatarUrl { get; init; }
    public int SurvivorUserId { get; init; }
    public int MergedUserId { get; init; }
}
