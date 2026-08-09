using System.Text;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
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

            var digest = await BuildDigestAsync(ct);
            var message = digest.Text;

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
        => (await BuildDigestAsync(ct)).Text;

    public async Task<FreeTierUnsubscribedVpnDigestResponse> BuildDigestAsync(CancellationToken ct = default)
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var overview = await overviewService.GetUnsubscribedConnectedAsync(ct);
        var candidates = overview.Candidates
            .OrderBy(c => c.DisplayName)
            .ToList();

        return new FreeTierUnsubscribedVpnDigestResponse
        {
            Text = BuildDigestMessage(todayUtc, candidates),
            Candidates = candidates,
        };
    }

    internal static string BuildDigestMessage(
        DateOnly dayUtc,
        IReadOnlyList<FreeTierEnforcementCandidateDto> users)
    {
        var merged = users.Where(u => u.IsMergedAccount).OrderBy(u => u.DisplayName).ToList();
        var notMerged = users.Where(u => !u.IsMergedAccount).OrderBy(u => u.DisplayName).ToList();
        var totalConnected = users.Count;

        var sb = new StringBuilder();
        sb.AppendLine("📅 Daily digest");
        sb.AppendLine("📋 Free/Default VPN without Telegram channel subscription");
        sb.AppendLine($"Date (UTC): {dayUtc:yyyy-MM-dd}");
        sb.AppendLine($"Currently online: {totalConnected}");
        sb.AppendLine($"Merged (linked): {merged.Count}");
        sb.AppendLine($"Not merged: {notMerged.Count}");

        if (totalConnected == 0)
        {
            sb.AppendLine();
            sb.AppendLine("No connected Free/Default users without channel subscription.");
            return sb.ToString();
        }

        AppendUserSection(sb, "🔗 Merged (linked accounts)", merged);
        AppendUserSection(sb, "👤 Not merged", notMerged);

        sb.AppendLine();
        sb.AppendLine("Remind TG: /remind_channel_subscribe <userId|telegramId>");
        sb.AppendLine("Remind email: tap Email #id below or /remind_channel_email <userId>");

        return sb.ToString();
    }

    internal static string FormatUserLine(FreeTierEnforcementCandidateDto user)
    {
        var tg = user.TelegramId is > 0 ? user.TelegramId.ToString() : "—";
        var server = string.IsNullOrWhiteSpace(user.VpnServerName) ? "?" : user.VpnServerName;
        var accountType = user.IdentityProviders is { Count: > 0 }
            ? string.Join("+", user.IdentityProviders)
            : "unknown";
        var parts = new List<string>
        {
            $"#{user.UserId} {user.DisplayName}",
            accountType,
        };
        if (!string.IsNullOrWhiteSpace(user.Email))
            parts.Add(user.Email.Trim());
        parts.Add($"TG:{tg}");
        parts.Add(server);
        return "• " + string.Join(" | ", parts);
    }

    private static void AppendUserSection(
        StringBuilder sb,
        string title,
        IReadOnlyList<FreeTierEnforcementCandidateDto> users)
    {
        sb.AppendLine();
        sb.AppendLine(title);
        if (users.Count == 0)
        {
            sb.AppendLine("— none —");
            return;
        }

        const int maxLines = 40;
        var shown = 0;
        foreach (var user in users)
        {
            if (shown >= maxLines)
            {
                sb.AppendLine($"… and {users.Count - maxLines} more");
                break;
            }

            sb.AppendLine(FormatUserLine(user));
            shown++;
        }
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
