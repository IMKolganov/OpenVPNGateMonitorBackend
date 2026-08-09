using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierUnsubscribedUserReminderService(
    ISettingsService settingsService,
    ITelegramDirectMessageSender telegramDirectMessageSender,
    IMemoryCache memoryCache,
    IOptions<TelegramChannelSettings> channelOptions,
    ILogger<FreeTierUnsubscribedUserReminderService> logger) : IFreeTierUnsubscribedUserReminderService
{
    /// <summary>Avoid spamming the same Telegram user with subscribe reminders.</summary>
    private static readonly TimeSpan ReminderCooldown = TimeSpan.FromHours(24);

    public async Task TryRemindAsync(long telegramId, string context, CancellationToken ct = default)
    {
        if (telegramId <= 0)
            return;

        try
        {
            if (!await IsEnabledAsync(ct))
                return;

            var cooldownKey = BuildCooldownKey(telegramId);
            if (memoryCache.TryGetValue(cooldownKey, out _))
                return;

            var channel = channelOptions.Value.RequiredChannelChatId;
            var text =
                $"📢 Please subscribe to {channel} to keep using Free/Default VPN access.\n\n" +
                $"Подпишитесь на канал {channel}, чтобы продолжать пользоваться VPN на тарифе Free/Default.";

            if (!await telegramDirectMessageSender.TrySendMessageAsync(telegramId, text, ct))
            {
                logger.LogInformation(
                    "Could not send unsubscribed reminder to TelegramId {TelegramId}. Context={Context}",
                    telegramId,
                    context);
                return;
            }

            memoryCache.Set(cooldownKey, true, ReminderCooldown);
            logger.LogInformation(
                "Sent channel-subscribe reminder to TelegramId {TelegramId}. Context={Context}",
                telegramId,
                context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send unsubscribed reminder to TelegramId {TelegramId}. Context={Context}",
                telegramId,
                context);
        }
    }

    private async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        var typeKey = $"{FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders}_Type";
        var type = await settingsService.GetValueAsync<string>(typeKey, ct);
        if (!string.Equals(type, "bool", StringComparison.OrdinalIgnoreCase))
            return true;

        return await settingsService.GetValueAsync<bool>(
            FreeTierAccessSettingsKeys.SendUnsubscribedUserReminders,
            ct);
    }

    internal static string BuildCooldownKey(long telegramId) => $"free-tier-unsub-reminder:{telegramId}";
}
