using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.EmailTemplates;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Enums;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierUnsubscribedUserReminderService(
    ISettingsService settingsService,
    ITelegramDirectMessageSender telegramDirectMessageSender,
    ILocalizationService localizationService,
    IUserIdentityLinkQueryService identityLinkQueryService,
    IUserQueryService userQueryService,
    IEmailSenderService emailSenderService,
    ISystemTransactionalEmailService systemTransactionalEmailService,
    ISentEmailLogService sentEmailLogService,
    IMemoryCache memoryCache,
    IOptions<TelegramChannelSettings> channelOptions,
    ILogger<FreeTierUnsubscribedUserReminderService> logger) : IFreeTierUnsubscribedUserReminderService
{
    public const string LocalizationKey = "FreeTierChannelSubscribeReminder";

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

            if (!await SendTelegramReminderAsync(telegramId, ct))
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

    public async Task<FreeTierChannelSubscribeRemindResponse> ForceRemindAsync(
        string target,
        FreeTierChannelSubscribeRemindChannel channel,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return FreeTierChannelSubscribeRemindResponse.Fail(
                channel,
                channel == FreeTierChannelSubscribeRemindChannel.Email
                    ? "Usage: /remind_channel_email <userId>"
                    : "Usage: /remind_channel_subscribe <userId|telegramId>");
        }

        target = target.Trim();
        if (target.StartsWith("tg:", StringComparison.OrdinalIgnoreCase))
            target = target["tg:".Length..].Trim();
        if (target.StartsWith("#"))
            target = target[1..].Trim();

        if (!long.TryParse(target, out var id) || id <= 0)
            return FreeTierChannelSubscribeRemindResponse.Fail(channel, "Invalid id. Use dashboard userId or Telegram id.");

        return channel switch
        {
            FreeTierChannelSubscribeRemindChannel.Email => await ForceRemindEmailAsync(id, ct),
            _ => await ForceRemindTelegramAsync(id, ct),
        };
    }

    private async Task<FreeTierChannelSubscribeRemindResponse> ForceRemindTelegramAsync(long id, CancellationToken ct)
    {
        int? userId = null;
        long telegramId;
        string resolvedAs;

        if (id <= int.MaxValue)
        {
            var links = await identityLinkQueryService.GetListByUserId((int)id, ct);
            var fromUser = FreeTierAccessComplianceService.TryGetTelegramId(links);
            if (fromUser is > 0)
            {
                userId = (int)id;
                telegramId = fromUser.Value;
                resolvedAs = $"user #{id} → TG:{telegramId}";
            }
            else
            {
                telegramId = id;
                resolvedAs = $"TG:{telegramId}";
            }
        }
        else
        {
            telegramId = id;
            resolvedAs = $"TG:{telegramId}";
        }

        try
        {
            if (!await SendTelegramReminderAsync(telegramId, ct))
            {
                return FreeTierChannelSubscribeRemindResponse.Fail(
                    FreeTierChannelSubscribeRemindChannel.Telegram,
                    $"Could not deliver Telegram reminder to {resolvedAs}. User may have blocked the bot.");
            }

            memoryCache.Set(BuildCooldownKey(telegramId), true, ReminderCooldown);
            logger.LogInformation("Admin forced Telegram channel-subscribe reminder to {ResolvedAs}", resolvedAs);
            return FreeTierChannelSubscribeRemindResponse.Ok(
                FreeTierChannelSubscribeRemindChannel.Telegram,
                $"✅ Telegram reminder sent to {resolvedAs}",
                userId,
                telegramId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Force Telegram channel-subscribe reminder failed for {ResolvedAs}", resolvedAs);
            return FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Telegram,
                $"Failed to send Telegram reminder to {resolvedAs}.");
        }
    }

    private async Task<FreeTierChannelSubscribeRemindResponse> ForceRemindEmailAsync(long id, CancellationToken ct)
    {
        if (id is <= 0 or > int.MaxValue)
        {
            return FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Email,
                "Email remind requires a dashboard userId.");
        }

        var userId = (int)id;
        var user = await userQueryService.GetById(userId, ct);
        if (user is null)
        {
            return FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Email,
                $"User #{userId} not found.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Email,
                $"User #{userId} has no email on file.");
        }

        var email = user.Email.Trim();
        var channelChat = channelOptions.Value.RequiredChannelChatId;
        var channelUrl = channelOptions.Value.RequiredChannelUrl;
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? $"User #{userId}" : user.DisplayName.Trim();

        try
        {
            var (subject, bodyHtml) = await systemTransactionalEmailService.GetFreeTierChannelSubscribeReminderAsync(
                displayName, channelChat, channelUrl, ct);

            string? sendError = null;
            try
            {
                await emailSenderService.SendAsync(email, subject, bodyHtml, ct);
            }
            catch (Exception ex)
            {
                sendError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                logger.LogWarning(ex, "Failed to send channel-subscribe reminder email to user {UserId}", userId);
            }

            try
            {
                await sentEmailLogService.LogAsync(
                    userId, email, subject, bodyHtml, sendError is null, sendError, null, ct);
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write SentEmailLog for channel-subscribe reminder user {UserId}", userId);
            }

            if (sendError is not null)
            {
                return FreeTierChannelSubscribeRemindResponse.Fail(
                    FreeTierChannelSubscribeRemindChannel.Email,
                    $"Failed to send email to {email}.");
            }

            logger.LogInformation("Admin forced email channel-subscribe reminder to user {UserId} ({Email})", userId, email);
            return FreeTierChannelSubscribeRemindResponse.Ok(
                FreeTierChannelSubscribeRemindChannel.Email,
                $"✅ Email reminder sent to user #{userId} ({email})",
                userId,
                email: email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Force email channel-subscribe reminder failed for user {UserId}", userId);
            return FreeTierChannelSubscribeRemindResponse.Fail(
                FreeTierChannelSubscribeRemindChannel.Email,
                $"Failed to send email reminder to user #{userId}.");
        }
    }

    internal async Task<string> BuildLocalizedReminderTextAsync(long telegramId, CancellationToken ct)
    {
        var channel = channelOptions.Value.RequiredChannelChatId;
        var channelUrl = channelOptions.Value.RequiredChannelUrl;
        var template = await localizationService.GetTextForTelegramUser(LocalizationKey, telegramId, ct);
        return ApplyPlaceholders(template, channel, channelUrl);
    }

    internal static string ApplyPlaceholders(string template, string channel, string channelUrl)
        => template
            .Replace("{channel}", channel, StringComparison.Ordinal)
            .Replace("{channelUrl}", channelUrl, StringComparison.Ordinal);

    private async Task<bool> SendTelegramReminderAsync(long telegramId, CancellationToken ct)
    {
        var text = await BuildLocalizedReminderTextAsync(telegramId, ct);
        return await telegramDirectMessageSender.TrySendMessageAsync(telegramId, text, ct);
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
