using DataGateMonitor.Services.TelegramBot.Interfaces;

namespace DataGateMonitor.Services.TelegramBot;

public sealed class TelegramAdminAlertService(
    ITelegramUserService telegramUserService,
    ITelegramBotUserProfilePhotoService profilePhotoService,
    ITelegramDirectMessageSender directMessageSender,
    IHttpClientFactory httpClientFactory,
    ILogger<TelegramAdminAlertService> logger) : ITelegramAdminAlertService
{
    private const int MaxRemoteAvatarBytes = 5 * 1024 * 1024;

    public async Task NotifyAccountsLinkedAsync(TelegramAccountsLinkedAlert alert, CancellationToken ct = default)
    {
        try
        {
            var admins = await telegramUserService.GetAdminsAsync(ct);
            if (admins.Count == 0)
            {
                logger.LogWarning("No Telegram bot admins configured; skipping accounts-linked alert.");
                return;
            }

            var caption = BuildAccountsLinkedCaption(alert);
            var telegramPhoto = await ResolveTelegramPhotoAsync(alert.TelegramId, ct);
            var linkedPhoto = await TryDownloadRemoteAvatarAsync(alert.LinkedAvatarUrl, ct);

            foreach (var admin in admins)
            {
                try
                {
                    if (telegramPhoto is { Length: > 0 } && linkedPhoto is { Length: > 0 })
                    {
                        await directMessageSender.TrySendPhotoAsync(
                            admin.TelegramId,
                            telegramPhoto,
                            caption + "\n\n📷 Telegram avatar",
                            "telegram-avatar.jpg",
                            ct);
                        await directMessageSender.TrySendPhotoAsync(
                            admin.TelegramId,
                            linkedPhoto,
                            $"📷 {alert.LinkedProviderLabel} avatar",
                            "linked-avatar.jpg",
                            ct);
                    }
                    else if (telegramPhoto is { Length: > 0 })
                    {
                        await directMessageSender.TrySendPhotoAsync(
                            admin.TelegramId,
                            telegramPhoto,
                            caption,
                            "telegram-avatar.jpg",
                            ct);
                    }
                    else if (linkedPhoto is { Length: > 0 })
                    {
                        await directMessageSender.TrySendPhotoAsync(
                            admin.TelegramId,
                            linkedPhoto,
                            caption,
                            "linked-avatar.jpg",
                            ct);
                    }
                    else
                    {
                        await directMessageSender.TrySendMessageAsync(admin.TelegramId, caption, ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to send accounts-linked alert to admin TelegramId {TelegramId}",
                        admin.TelegramId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify admins about accounts linked for TelegramId {TelegramId}",
                alert.TelegramId);
        }
    }

    private async Task<byte[]?> ResolveTelegramPhotoAsync(long telegramId, CancellationToken ct)
    {
        var live = await directMessageSender.TryDownloadUserProfilePhotoAsync(telegramId, ct);
        if (live is { Length: > 0 })
            return live;

        var stored = await profilePhotoService.GetImageByTelegramIdAsync(telegramId, ct);
        return stored?.Bytes is { Length: > 0 } ? stored.Value.Bytes : null;
    }

    private async Task<byte[]?> TryDownloadRemoteAvatarAsync(string? avatarUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl)
            || !Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > MaxRemoteAvatarBytes)
                return null;

            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download linked-account avatar from {AvatarUrl}", avatarUrl);
            return null;
        }
    }

    internal static string BuildAccountsLinkedCaption(TelegramAccountsLinkedAlert alert)
    {
        var tgName = string.IsNullOrWhiteSpace(alert.TelegramDisplayName)
            ? "Unnamed"
            : alert.TelegramDisplayName.Trim();
        var username = string.IsNullOrWhiteSpace(alert.TelegramUsername)
            ? "@"
            : "@" + alert.TelegramUsername.Trim().TrimStart('@');

        var lines = new List<string>
        {
            "🔗 Accounts linked",
            $"Telegram ID: `{alert.TelegramId}`",
            $"Username: {username}",
            $"Name: {tgName}",
            "",
            $"Linked: {alert.LinkedProviderLabel}",
        };

        if (!string.IsNullOrWhiteSpace(alert.LinkedDisplayName))
            lines.Add($"Name: {alert.LinkedDisplayName.Trim()}");
        if (!string.IsNullOrWhiteSpace(alert.LinkedEmail))
            lines.Add($"Email: {alert.LinkedEmail.Trim()}");
        if (!string.IsNullOrWhiteSpace(alert.LinkedExternalId))
            lines.Add($"External ID: `{alert.LinkedExternalId.Trim()}`");

        lines.Add("");
        lines.Add($"Survivor user #: {alert.SurvivorUserId}");
        lines.Add($"Merged user #: {alert.MergedUserId}");

        return string.Join('\n', lines);
    }
}
