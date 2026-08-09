using System.Text;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using Microsoft.Extensions.Caching.Memory;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierUnsubscribedVpnUsersDailyDigestService(
    ISettingsService settingsService,
    IFreeTierEnforcementOverviewService overviewService,
    ITelegramUserService telegramUserService,
    ITelegramDirectMessageSender telegramDirectMessageSender,
    IMemoryCache memoryCache,
    ILogger<FreeTierUnsubscribedVpnUsersDailyDigestService> logger)
    : IFreeTierUnsubscribedVpnUsersDailyDigestService
{
    private const string LastSentUtcDateCacheKey = "free-tier-unsub-admin-digest:last-utc-date";

    public async Task TrySendDailyDigestAsync(CancellationToken ct = default)
    {
        try
        {
            if (!await IsEnabledAsync(ct))
            {
                logger.LogDebug("Daily unsubscribed VPN-users admin digest is disabled.");
                return;
            }

            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            if (memoryCache.TryGetValue(LastSentUtcDateCacheKey, out DateOnly lastSent) && lastSent == todayUtc)
                return;

            var message = await BuildDigestTextAsync(ct);

            var admins = await telegramUserService.GetAdminsAsync(ct);
            if (admins.Count == 0)
            {
                logger.LogWarning("No Telegram bot admins configured; skipping unsubscribed VPN digest.");
                return;
            }

            var anySent = false;
            foreach (var admin in admins)
            {
                if (await telegramDirectMessageSender.TrySendMessageAsync(admin.TelegramId, message, ct))
                    anySent = true;
            }

            if (!anySent)
            {
                logger.LogWarning("Failed to deliver unsubscribed VPN digest to any admin.");
                return;
            }

            var untilTomorrow = todayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DateTime.UtcNow;
            if (untilTomorrow < TimeSpan.FromMinutes(1))
                untilTomorrow = TimeSpan.FromMinutes(1);

            memoryCache.Set(LastSentUtcDateCacheKey, todayUtc, untilTomorrow);
            logger.LogInformation("Sent daily unsubscribed VPN digest to admins.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send daily unsubscribed VPN-users admin digest.");
        }
    }

    public async Task<string> BuildDigestTextAsync(CancellationToken ct = default)
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var overview = await overviewService.GetUnsubscribedConnectedAsync(ct);
        var connectedUnsubscribed = overview.Candidates
            .OrderBy(c => c.DisplayName)
            .ToList();

        return BuildDigestMessage(todayUtc, connectedUnsubscribed.Count, connectedUnsubscribed);
    }

    internal static string BuildDigestMessage(
        DateOnly dayUtc,
        int totalConnected,
        IReadOnlyList<FreeTierEnforcementCandidateDto> users)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 Free/Default VPN without Telegram channel subscription");
        sb.AppendLine($"Date (UTC): {dayUtc:yyyy-MM-dd}");
        sb.AppendLine($"Currently online: {totalConnected}");

        if (totalConnected == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No connected Free/Default users without channel subscription.");
            return sb.ToString();
        }

        sb.AppendLine();
        const int maxLines = 40;
        var shown = 0;
        foreach (var user in users)
        {
            if (shown >= maxLines)
            {
                sb.AppendLine($"… and {totalConnected - maxLines} more");
                break;
            }

            var tg = user.TelegramId is > 0 ? user.TelegramId.ToString() : "—";
            var server = string.IsNullOrWhiteSpace(user.VpnServerName) ? "?" : user.VpnServerName;
            var merged = user.IsMergedAccount ? "merged" : "not-merged";
            sb.AppendLine($"• #{user.UserId} {user.DisplayName} | TG:{tg} | {server} | {merged}");
            shown++;
        }

        return sb.ToString();
    }

    private async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        var typeKey = $"{FreeTierAccessSettingsKeys.DailyUnsubscribedAdminDigest}_Type";
        var type = await settingsService.GetValueAsync<string>(typeKey, ct);
        if (!string.Equals(type, "bool", StringComparison.OrdinalIgnoreCase))
            return true;

        return await settingsService.GetValueAsync<bool>(
            FreeTierAccessSettingsKeys.DailyUnsubscribedAdminDigest,
            ct);
    }
}
