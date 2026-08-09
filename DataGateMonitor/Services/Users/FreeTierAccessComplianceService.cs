using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.Others;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierAccessComplianceService(
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanQueryService quotaPlanQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    ITelegramChannelMembershipChecker telegramChannelMembershipChecker,
    IFreeTierUnsubscribedUserReminderService unsubscribedUserReminderService,
    IAppNotificationFacade appNotificationFacade,
    ISettingsService settingsService,
    IMemoryCache memoryCache,
    IOptions<TelegramChannelSettings> channelOptions,
    ILogger<FreeTierAccessComplianceService> logger) : IFreeTierAccessComplianceService
{
    private const string GoogleProvider = "google";
    private const string LocalProvider = "local";
    private const string TelegramProvider = "telegram";

    public async Task<FreeTierAccessStatusResponse> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var evaluation = await EvaluateAccessAsync(userId, isChannelSubscribed: null, ct);
        var result = evaluation.Result;
        var links = evaluation.Links;

        if (result is { IsApplicable: true, IsCompliant: false } && TryGetGraceExpiresAt(userId) is { } expiresAt)
        {
            result = CopyResult(result, isCompliant: true, isGracePeriod: true, graceExpiresAtUtc: expiresAt);
        }

        return MapToStatusResponse(result, links);
    }

    /// <summary>
    /// Called by client apps right after establishing a VPN connection. Starts/refreshes the grace
    /// window (same effect as <see cref="AuditAndNotifyIfNeededAsync"/>) and returns the resulting status,
    /// including <see cref="FreeTierAccessStatusResponse.GraceExpiresAtUtc"/> for a "connected for N minutes" UI.
    /// </summary>
    public async Task<FreeTierAccessStatusResponse> RegisterConnectionAsync(
        int userId, string context, CancellationToken ct = default)
    {
        var (result, links) = await AuditAndNotifyCoreAsync(userId, context, isChannelSubscribed: null, ct);
        return MapToStatusResponse(result, links);
    }

    public async Task<FreeTierAccessComplianceResult> AuditAndNotifyIfNeededByTelegramIdAsync(
        long telegramId,
        string context,
        bool? isChannelSubscribed = null,
        CancellationToken ct = default)
    {
        if (telegramId <= 0)
        {
            return new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = false,
                TelegramId = telegramId,
            };
        }

        var link = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider,
            telegramId.ToString(),
            ct);

        if (link is not { UserId: > 0 })
        {
            return new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = false,
                TelegramId = telegramId,
            };
        }

        var result = await AuditAndNotifyIfNeededAsync(link.UserId, context, isChannelSubscribed, ct);
        if (telegramId > 0)
            result.TelegramId = telegramId;
        return result;
    }

    public async Task<bool> ShouldEnforceOpenVpnDisconnectAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0)
            return false;

        var result = await EvaluateAccessForEnforcementAsync(userId, ct);
        return result is { IsApplicable: true, IsCompliant: false };
    }

    public async Task<FreeTierAccessComplianceResult> EvaluateAccessForEnforcementAsync(
        int userId, CancellationToken ct = default)
    {
        var (result, _) = await EvaluateAccessAsync(userId, isChannelSubscribed: null, ct);
        if (result is { IsApplicable: true, IsCompliant: false } && TryGetGraceExpiresAt(userId) is { } expiresAt)
            return CopyResult(result, isCompliant: true, isGracePeriod: true, graceExpiresAtUtc: expiresAt);

        return result;
    }

    public async Task<FreeTierAccessComplianceResult> AuditAndNotifyIfNeededAsync(
        int userId,
        string context,
        bool? isChannelSubscribed = null,
        CancellationToken ct = default)
    {
        var (result, _) = await AuditAndNotifyCoreAsync(userId, context, isChannelSubscribed, ct);
        return result;
    }

    private async Task<(FreeTierAccessComplianceResult Result, IReadOnlyList<UserIdentityLink> Links)> AuditAndNotifyCoreAsync(
        int userId,
        string context,
        bool? isChannelSubscribed,
        CancellationToken ct)
    {
        var evaluation = await EvaluateAccessAsync(userId, isChannelSubscribed, ct);
        var result = evaluation.Result;
        var links = evaluation.Links;

        if (result is { IsApplicable: true, IsChannelSubscribed: false, TelegramId: > 0 })
        {
            await unsubscribedUserReminderService.TryRemindAsync(
                result.TelegramId.Value,
                context,
                ct);
        }

        if (!result.IsApplicable || result.IsCompliant)
            return (result, links);

        if (await TryApplyGracePeriodAsync(userId, ct) is { } expiresAt)
        {
            logger.LogInformation(
                "User {UserId} on plan {PlanName} allowed via grace period. Context={Context}",
                userId,
                result.ActivePlanName,
                context);

            return (CopyResult(result, isCompliant: true, isGracePeriod: true, graceExpiresAtUtc: expiresAt), links);
        }

        var cooldownKey = BuildAdminNotifyCooldownKey(userId);
        if (!memoryCache.TryGetValue(cooldownKey, out _))
        {
            memoryCache.Set(cooldownKey, true, AdminNotifyCooldown);

            try
            {
                await appNotificationFacade.FreeTierAccessNonCompliant(
                    userId,
                    result.ActivePlanName ?? QuotaPlanNames.Free,
                    result.TelegramId,
                    result.IsMergedAccount,
                    result.IsChannelSubscribed,
                    context,
                    channelOptions.Value.RequiredChannelChatId,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify admins about Free/Default access violation for user {UserId}", userId);
            }
        }

        logger.LogWarning(
            "User {UserId} on plan {PlanName} is not subscribed to {Channel}. Merged={IsMerged}. Context={Context}",
            userId,
            result.ActivePlanName,
            channelOptions.Value.RequiredChannelChatId,
            result.IsMergedAccount,
            context);

        return (CopyResult(result, adminsNotified: true), links);
    }

    private static FreeTierAccessComplianceResult CopyResult(
        FreeTierAccessComplianceResult source,
        bool? isCompliant = null,
        bool? isGracePeriod = null,
        bool? adminsNotified = null,
        DateTimeOffset? graceExpiresAtUtc = null)
        => new()
        {
            IsApplicable = source.IsApplicable,
            IsCompliant = isCompliant ?? source.IsCompliant,
            IsMergedAccount = source.IsMergedAccount,
            IsChannelSubscribed = source.IsChannelSubscribed,
            IsGracePeriod = isGracePeriod ?? source.IsGracePeriod,
            ActivePlanName = source.ActivePlanName,
            UserId = source.UserId,
            TelegramId = source.TelegramId,
            AdminsNotified = adminsNotified ?? source.AdminsNotified,
            GraceExpiresAtUtc = graceExpiresAtUtc ?? source.GraceExpiresAtUtc,
        };

    private async Task<(FreeTierAccessComplianceResult Result, IReadOnlyList<UserIdentityLink> Links)> EvaluateAccessAsync(
        int userId,
        bool? isChannelSubscribed,
        CancellationToken ct)
    {
        var activeAssignment = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        if (activeAssignment is null)
        {
            return (
                new FreeTierAccessComplianceResult
                {
                    IsApplicable = false,
                    IsCompliant = true,
                    UserId = userId,
                },
                []);
        }

        var plan = await quotaPlanQueryService.GetById(activeAssignment.QuotaPlanId, ct);
        if (plan is null || !QuotaPlanNames.IsFreeOrDefault(plan.Name))
        {
            return (
                new FreeTierAccessComplianceResult
                {
                    IsApplicable = false,
                    IsCompliant = true,
                    UserId = userId,
                    ActivePlanName = plan?.Name,
                },
                []);
        }

        var links = await userIdentityLinkQueryService.GetListByUserId(userId, ct);
        var isMerged = IsMergedAccount(links);
        var telegramId = TryGetTelegramId(links);

        var channelSubscribed = isChannelSubscribed;
        if (channelSubscribed != true && telegramId is > 0)
            channelSubscribed = await telegramChannelMembershipChecker.IsSubscribedAsync(telegramId.Value, ct);

        // Free/Default always require an active Telegram channel subscription.
        // Linking Telegram+Google/password does not exempt the user.
        var isCompliant = channelSubscribed == true;

        return (
            new FreeTierAccessComplianceResult
            {
                IsApplicable = true,
                IsCompliant = isCompliant,
                IsMergedAccount = isMerged,
                IsChannelSubscribed = channelSubscribed == true,
                ActivePlanName = plan.Name,
                UserId = userId,
                TelegramId = telegramId,
            },
            links);
    }

    private FreeTierAccessStatusResponse MapToStatusResponse(
        FreeTierAccessComplianceResult result,
        IReadOnlyList<UserIdentityLink> links)
    {
        var isLinkedToTelegram = links.Any(l =>
            string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase));
        var hasGoogle = links.Any(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        var hasLocal = links.Any(l =>
            string.Equals(l.Provider, LocalProvider, StringComparison.OrdinalIgnoreCase));

        return new FreeTierAccessStatusResponse
        {
            IsApplicable = result.IsApplicable,
            IsCompliant = result.IsCompliant,
            IsMergedAccount = result.IsMergedAccount,
            IsChannelSubscribed = result.IsChannelSubscribed,
            IsGracePeriod = result.IsGracePeriod,
            GraceExpiresAtUtc = result.GraceExpiresAtUtc,
            IsLinkedToTelegram = isLinkedToTelegram,
            CanRequestAccountLinkCode = !isLinkedToTelegram && (hasGoogle || hasLocal),
            ActivePlanName = result.ActivePlanName,
            RequiredChannel = channelOptions.Value.RequiredChannelChatId,
        };
    }

    internal static bool IsMergedAccount(IReadOnlyCollection<UserIdentityLink> links)
    {
        var hasTelegram = links.Any(l =>
            string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase));
        var hasGoogle = links.Any(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        var hasLocal = links.Any(l =>
            string.Equals(l.Provider, LocalProvider, StringComparison.OrdinalIgnoreCase));
        return hasTelegram && (hasGoogle || hasLocal);
    }

    internal static long? TryGetTelegramId(IReadOnlyCollection<UserIdentityLink> links)
    {
        var externalId = links
            .FirstOrDefault(l => string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase))
            ?.ExternalId;

        return long.TryParse(externalId, out var telegramId) && telegramId > 0 ? telegramId : null;
    }

    internal static string BuildGraceCacheKey(int userId) => $"free-tier-grace:{userId}";

    private static string BuildAdminNotifyCooldownKey(int userId) => $"free-tier-admin-notify-cooldown:{userId}";

    /// <summary>
    /// Suppresses repeat "non-compliant" admin notifications for the same user within this window.
    /// Callers of <see cref="AuditAndNotifyIfNeededAsync"/> can legitimately fire many times in a row
    /// for the same non-compliant user (e.g. a client app calling <c>RegisterConnectionAsync</c> on
    /// every reconnect attempt) — without this, each call would page admins again.
    /// </summary>
    private static readonly TimeSpan AdminNotifyCooldown = TimeSpan.FromMinutes(10);

    private DateTimeOffset? TryGetGraceExpiresAt(int userId)
        => memoryCache.TryGetValue(BuildGraceCacheKey(userId), out DateTimeOffset expiresAt) ? expiresAt : null;

    private async Task<DateTimeOffset?> TryApplyGracePeriodAsync(int userId, CancellationToken ct)
    {
        if (!await IsGraceEnabledAsync(ct))
            return null;

        var graceMinutes = await GetGraceMinutesAsync(ct);
        if (graceMinutes <= 0)
            return null;

        var cacheKey = BuildGraceCacheKey(userId);
        if (TryGetGraceExpiresAt(userId) is { } existingExpiresAt)
            return existingExpiresAt;

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(graceMinutes);
        memoryCache.Set(
            cacheKey,
            expiresAt,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(graceMinutes),
            });

        return expiresAt;
    }

    private async Task<bool> IsGraceEnabledAsync(CancellationToken ct)
    {
        var typeKey = $"{FreeTierAccessSettingsKeys.AllowGraceWithoutCompliance}_Type";
        var type = await settingsService.GetValueAsync<string>(typeKey, ct);
        if (!string.Equals(type, "bool", StringComparison.OrdinalIgnoreCase))
            return false;

        return await settingsService.GetValueAsync<bool>(
            FreeTierAccessSettingsKeys.AllowGraceWithoutCompliance,
            ct);
    }

    private async Task<int> GetGraceMinutesAsync(CancellationToken ct)
    {
        var typeKey = $"{FreeTierAccessSettingsKeys.GracePeriodMinutes}_Type";
        var type = await settingsService.GetValueAsync<string>(typeKey, ct);
        if (!string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
            return 5;

        var minutes = await settingsService.GetValueAsync<int>(
            FreeTierAccessSettingsKeys.GracePeriodMinutes,
            ct);

        return minutes > 0 ? minutes : 5;
    }
}
