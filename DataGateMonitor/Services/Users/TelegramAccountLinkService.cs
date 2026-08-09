using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses;

namespace DataGateMonitor.Services.Users;

public sealed class TelegramAccountLinkService(
    IMemoryCache cache,
    IUserQueryService userQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IUserMergeService userMergeService,
    ITelegramUserService telegramUserService,
    ITelegramAdminAlertService telegramAdminAlertService,
    IConfiguration configuration,
    ILogger<TelegramAccountLinkService> logger) : ITelegramAccountLinkService
{
    private const int DefaultCodeExpirationMinutes = 15;
    private const int CodeLength = 8;
    /// <summary>Cache sentinel: Telegram account is chosen when the bot completes the code.</summary>
    private const long BindTelegramAtBotCompletion = 0;
    /// <summary>Cache sentinel: dashboard user is chosen when the app completes the code.</summary>
    private const int BindDashboardAtAppCompletion = 0;

    private const string TelegramProvider = AuthIdentityProviders.Telegram;
    private const string GoogleProvider = AuthIdentityProviders.Google;
    private const string LocalProvider = AuthIdentityProviders.Local;

    /// <summary>Bot-localized via LocalizationTexts key <c>AccountLinkTelegramAlreadyLinkedToGoogle</c>.</summary>
    public const string TelegramAlreadyLinkedToGooglePrefix = "TelegramAlreadyLinkedToGoogle|";

    private sealed record AccountLinkCacheEntry(int DashboardUserId, long ExpectedTelegramId);

    public async Task<RequestTelegramAccountLinkCodeResponse> RequestLinkCodeAsync(
        int userId,
        long? telegramId,
        CancellationToken ct)
    {
        if (userId <= 0)
            throw new ArgumentException("User id is required.", nameof(userId));

        await EnsureDashboardUserCanRequestLinkCodeAsync(userId, ct);

        var bindTelegramId = telegramId is > 0 ? telegramId.Value : BindTelegramAtBotCompletion;

        if (bindTelegramId > 0)
        {
            var existingTelegramLink = await userIdentityLinkQueryService.GetByProviderAndExternalId(
                TelegramProvider,
                bindTelegramId.ToString(),
                ct);

            if (existingTelegramLink is not { UserId: > 0 })
                throw new InvalidOperationException(
                    "Telegram account is not registered. Use /register in the bot first.");
        }

        InvalidateActiveCodeForUser(userId);

        var (code, expiry) = StoreCode(new AccountLinkCacheEntry(userId, bindTelegramId), UserActiveCodeKey(userId));

        logger.LogInformation(
            "Account link code issued for dashboard user {UserId}, bindTelegramAtBot={BindAtBot}, TelegramId={TelegramId}, valid {Minutes} minutes",
            userId,
            bindTelegramId == BindTelegramAtBotCompletion,
            bindTelegramId,
            expiry.TotalMinutes);

        return new RequestTelegramAccountLinkCodeResponse
        {
            Code = code,
            ExpiresInSeconds = (int)expiry.TotalSeconds,
        };
    }

    public async Task<RequestTelegramAccountLinkCodeResponse> RequestLinkCodeFromBotAsync(
        long telegramId,
        CancellationToken ct)
    {
        if (telegramId <= 0)
            throw new ArgumentException("Telegram id is required.", nameof(telegramId));

        var telegramLink = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider,
            telegramId.ToString(),
            ct);

        if (telegramLink is not { UserId: > 0 })
            throw new InvalidOperationException(
                "Telegram account is not registered. Use /register in the bot first.");

        InvalidateActiveCodeForTelegram(telegramId);

        var (code, expiry) = StoreCode(
            new AccountLinkCacheEntry(BindDashboardAtAppCompletion, telegramId),
            TelegramActiveCodeKey(telegramId));

        logger.LogInformation(
            "Account link code issued from bot for TelegramId={TelegramId}, valid {Minutes} minutes",
            telegramId,
            expiry.TotalMinutes);

        return new RequestTelegramAccountLinkCodeResponse
        {
            Code = code,
            ExpiresInSeconds = (int)expiry.TotalSeconds,
        };
    }

    public Task<CompleteTelegramAccountLinkResponse> CompleteLinkFromAppAsync(
        int userId,
        string code,
        CancellationToken ct)
    {
        if (userId <= 0)
            return Task.FromResult(Fail("User id is required."));

        var normalizedCode = NormalizeCode(code);
        if (normalizedCode is null)
            return Task.FromResult(Fail("Link code is required."));

        if (!cache.TryGetValue(AccountLinkCacheKey(normalizedCode), out AccountLinkCacheEntry? entry)
            || entry is null)
            return Task.FromResult(Fail("Invalid or expired link code."));

        if (entry.DashboardUserId != BindDashboardAtAppCompletion)
            return Task.FromResult(Fail("This code must be entered in the Telegram bot, not in the app."));

        return CompleteMergeAsync(
            normalizedCode,
            entry.ExpectedTelegramId,
            dashboardUserId: userId,
            performedByUserId: userId,
            notePrefix: "Telegram bot account link (app completed)",
            ct);
    }

    public async Task<CompleteTelegramAccountLinkResponse> CompleteLinkByCodeAsync(
        string code,
        long telegramId,
        CancellationToken ct)
    {
        if (telegramId <= 0)
            return Fail("Telegram id is required.");

        var normalizedCode = NormalizeCode(code);
        if (normalizedCode is null)
            return Fail("Link code is required.");

        if (!cache.TryGetValue(AccountLinkCacheKey(normalizedCode), out AccountLinkCacheEntry? entry)
            || entry is null)
        {
            // Most likely cause: this code was already redeemed by an earlier, successful
            // submission (e.g. the client retried after a slow/lost response) and the code was
            // removed from the cache on success. Tell the user they're already linked instead of
            // an unhelpful "invalid code" when that's the case.
            if (await IsAlreadyMergedByTelegramIdAsync(telegramId, ct))
            {
                return new CompleteTelegramAccountLinkResponse
                {
                    Success = true,
                    Message = "Accounts are already linked.",
                };
            }

            return Fail("Invalid or expired link code.");
        }

        if (entry.DashboardUserId == BindDashboardAtAppCompletion)
            return Fail("This code must be entered in the mobile app, not in the bot.");

        if (entry.ExpectedTelegramId != BindTelegramAtBotCompletion
            && entry.ExpectedTelegramId != telegramId)
            return Fail("This link code was issued for a different Telegram account.");

        var telegramLink = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider,
            telegramId.ToString(),
            ct);

        if (telegramLink is not { UserId: > 0 })
            return Fail("Telegram account is not registered. Use /register in the bot first.");

        return await CompleteMergeAsync(
            normalizedCode,
            telegramId,
            dashboardUserId: entry.DashboardUserId,
            performedByUserId: telegramLink.UserId,
            notePrefix: "Telegram bot account link (bot completed)",
            ct);
    }

    private async Task<CompleteTelegramAccountLinkResponse> CompleteMergeAsync(
        string normalizedCode,
        long telegramId,
        int dashboardUserId,
        int performedByUserId,
        string notePrefix,
        CancellationToken ct)
    {
        var telegramLink = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider,
            telegramId.ToString(),
            ct);

        if (telegramLink is not { UserId: > 0 })
            return Fail("Telegram account is not registered. Use /register in the bot first.");

        var telegramUserId = telegramLink.UserId;

        if (telegramUserId == dashboardUserId)
        {
            RemoveCode(normalizedCode, entryDashboardUserId: dashboardUserId, entryTelegramId: telegramId);
            return new CompleteTelegramAccountLinkResponse
            {
                Success = true,
                Message = "Accounts are already linked.",
            };
        }

        var dashboardUser = await userQueryService.GetById(dashboardUserId, ct);
        if (dashboardUser is null)
        {
            // The dashboard-side user row is hard-deleted once a merge completes (see
            // UserMergeService.MergeTelegramGoogleAsync). A null lookup here almost always means
            // this exact code was already redeemed successfully by an earlier submission and the
            // caller is retrying (e.g. a lost/timed-out response) — confirm via the survivor's
            // links and report success instead of a confusing "not found".
            if (await IsUserAlreadyMergedAsync(telegramUserId, ct))
            {
                RemoveCode(normalizedCode, entryDashboardUserId: dashboardUserId, entryTelegramId: telegramId);
                return new CompleteTelegramAccountLinkResponse
                {
                    Success = true,
                    Message = "Accounts are already linked.",
                };
            }

            return Fail("Dashboard user not found.");
        }

        if (dashboardUser.IsBlocked)
            return Fail("User account is blocked.");

        var dashboardLinks = await userIdentityLinkQueryService.GetListByUserId(dashboardUserId, ct);
        var dashboardTelegramLink = dashboardLinks.FirstOrDefault(l =>
            string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase));

        if (dashboardTelegramLink is not null
            && !string.Equals(dashboardTelegramLink.ExternalId, telegramId.ToString(), StringComparison.Ordinal))
        {
            return Fail("This dashboard account is already linked to a different Telegram account.");
        }

        var hasGoogle = dashboardLinks.Any(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        var hasLocal = dashboardLinks.Any(l =>
            string.Equals(l.Provider, LocalProvider, StringComparison.OrdinalIgnoreCase));

        if (!hasGoogle && !hasLocal)
            return Fail("Dashboard account has no Google or password identity to merge.");

        var googleConflictMessage = await TryBuildTelegramGoogleConflictMessageAsync(
            telegramUserId,
            dashboardLinks,
            ct);
        if (googleConflictMessage is not null)
        {
            logger.LogWarning(
                "Account link merge blocked for TelegramId={TelegramId}, DashboardUserId={DashboardUserId}: {Reason}",
                telegramId,
                dashboardUserId,
                googleConflictMessage);
            return Fail(googleConflictMessage);
        }

        try
        {
            var merge = await userMergeService.MergeTelegramGoogleAsync(
                new MergeTelegramGoogleUsersRequest
                {
                    TelegramUserId = telegramUserId,
                    GoogleUserId = dashboardUserId,
                    Note = $"{notePrefix} (telegramId={telegramId})",
                },
                performedByUserId: performedByUserId,
                ct);

            RemoveCode(normalizedCode, dashboardUserId, telegramId);

            await TryNotifyAdminsAccountsLinkedAsync(
                telegramId,
                dashboardUser,
                dashboardLinks,
                hasGoogle,
                merge,
                ct);

            return new CompleteTelegramAccountLinkResponse
            {
                Success = true,
                Message = "Accounts linked successfully.",
                Merge = merge,
            };
        }
        catch (InvalidOperationException ex) when (IsSurvivorGoogleConflict(ex))
        {
            var survivor = await userQueryService.GetById(telegramUserId, ct);
            var message = BuildTelegramAlreadyLinkedToGoogleMessage(survivor);
            logger.LogWarning(
                "Account link merge blocked for TelegramId={TelegramId}, DashboardUserId={DashboardUserId}: {Reason}",
                telegramId,
                dashboardUserId,
                message);
            return Fail(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Account link merge failed for TelegramId={TelegramId}, DashboardUserId={DashboardUserId}",
                telegramId,
                dashboardUserId);

            return Fail("Account link failed. Please try again or request a new code.");
        }
    }

    private async Task TryNotifyAdminsAccountsLinkedAsync(
        long telegramId,
        User dashboardUser,
        IReadOnlyList<UserIdentityLink> dashboardLinks,
        bool hasGoogle,
        MergeTelegramGoogleUsersResponse merge,
        CancellationToken ct)
    {
        try
        {
            var tgBotUser = await telegramUserService.GetUserByTelegramIdAsync(telegramId, ct);
            var tgFullName = $"{tgBotUser?.FirstName} {tgBotUser?.LastName}".Trim();
            var providerLabel = hasGoogle ? "Google" : "Password";
            var linkedExternalId = dashboardLinks.FirstOrDefault(l =>
                    string.Equals(
                        l.Provider,
                        hasGoogle ? GoogleProvider : LocalProvider,
                        StringComparison.OrdinalIgnoreCase))
                ?.ExternalId;

            await telegramAdminAlertService.NotifyAccountsLinkedAsync(
                new TelegramAccountsLinkedAlert
                {
                    TelegramId = telegramId,
                    TelegramUsername = tgBotUser?.Username,
                    TelegramDisplayName = string.IsNullOrWhiteSpace(tgFullName) ? null : tgFullName,
                    LinkedProviderLabel = providerLabel,
                    LinkedDisplayName = dashboardUser.DisplayName,
                    LinkedEmail = dashboardUser.Email,
                    LinkedExternalId = linkedExternalId,
                    LinkedAvatarUrl = dashboardUser.AvatarUrl,
                    SurvivorUserId = merge.SurvivorUserId,
                    MergedUserId = merge.MergedUserId,
                },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Accounts-linked admin alert failed for TelegramId={TelegramId}",
                telegramId);
        }
    }

    private async Task<string?> TryBuildTelegramGoogleConflictMessageAsync(
        int survivorUserId,
        IReadOnlyList<UserIdentityLink> incomingDashboardLinks,
        CancellationToken ct)
    {
        var survivorLinks = await userIdentityLinkQueryService.GetListByUserId(survivorUserId, ct);
        var survivorGoogleLink = survivorLinks.FirstOrDefault(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        if (survivorGoogleLink is null)
            return null;

        var incomingGoogleLink = incomingDashboardLinks.FirstOrDefault(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        if (incomingGoogleLink is null)
            return null;

        if (string.Equals(survivorGoogleLink.ExternalId, incomingGoogleLink.ExternalId, StringComparison.Ordinal))
            return null;

        var survivor = await userQueryService.GetById(survivorUserId, ct);
        return BuildTelegramAlreadyLinkedToGoogleMessage(survivor);
    }

    private static bool IsSurvivorGoogleConflict(InvalidOperationException ex)
        => ex.Message.Contains("already has a different Google identity link", StringComparison.Ordinal);

    private static string BuildTelegramAlreadyLinkedToGoogleMessage(User? survivor)
    {
        var label = FormatLinkedGoogleAccountLabel(survivor);
        return $"{TelegramAlreadyLinkedToGooglePrefix}{label}";
    }

    private static string FormatLinkedGoogleAccountLabel(User? user)
    {
        if (user is null)
            return "another Google account";

        var name = user.DisplayName?.Trim();
        var email = user.Email?.Trim();

        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email))
            return $"{name} ({email})";

        if (!string.IsNullOrEmpty(email))
            return email;

        if (!string.IsNullOrEmpty(name))
            return name;

        return "another Google account";
    }

    /// <summary>True when <paramref name="userId"/> already has both a Telegram link and a Google/local link.</summary>
    private async Task<bool> IsUserAlreadyMergedAsync(int userId, CancellationToken ct)
    {
        var links = await userIdentityLinkQueryService.GetListByUserId(userId, ct);
        var hasTelegram = links.Any(l => string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase));
        var hasGoogleOrLocal = links.Any(l =>
            string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(l.Provider, LocalProvider, StringComparison.OrdinalIgnoreCase));
        return hasTelegram && hasGoogleOrLocal;
    }

    private async Task<bool> IsAlreadyMergedByTelegramIdAsync(long telegramId, CancellationToken ct)
    {
        var telegramLink = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider,
            telegramId.ToString(),
            ct);

        return telegramLink is { UserId: > 0 } && await IsUserAlreadyMergedAsync(telegramLink.UserId, ct);
    }

    private async Task EnsureDashboardUserCanRequestLinkCodeAsync(int userId, CancellationToken ct)
    {
        var user = await userQueryService.GetById(userId, ct)
                   ?? throw new KeyNotFoundException("User not found.");

        if (user.IsBlocked)
            throw new InvalidOperationException("User account is blocked.");

        var links = await userIdentityLinkQueryService.GetListByUserId(userId, ct);

        if (links.Any(l => string.Equals(l.Provider, TelegramProvider, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This account is already linked to Telegram.");

        var hasGoogle = links.Any(l => string.Equals(l.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase));
        var hasLocal = links.Any(l => string.Equals(l.Provider, LocalProvider, StringComparison.OrdinalIgnoreCase));

        if (!hasGoogle && !hasLocal)
            throw new InvalidOperationException(
                "This account has no Google or password identity to link. Sign in with Google or register with a password first.");
    }

    private (string Code, TimeSpan Expiry) StoreCode(AccountLinkCacheEntry entry, string activeKey)
    {
        var code = GenerateCode();
        var minutes = configuration.GetValue<int?>("Auth:TelegramAccountLinkCodeMinutes") ?? DefaultCodeExpirationMinutes;
        if (minutes <= 0)
            minutes = DefaultCodeExpirationMinutes;

        var expiry = TimeSpan.FromMinutes(minutes);
        var cacheOptions = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };

        cache.Set(AccountLinkCacheKey(code), entry, cacheOptions);
        cache.Set(activeKey, code, cacheOptions);

        return (code, expiry);
    }

    private void InvalidateActiveCodeForUser(int userId)
    {
        if (!cache.TryGetValue(UserActiveCodeKey(userId), out string? previousCode)
            || string.IsNullOrWhiteSpace(previousCode))
        {
            return;
        }

        if (cache.TryGetValue(AccountLinkCacheKey(previousCode), out AccountLinkCacheEntry? entry) && entry is not null)
            RemoveCode(previousCode, entry.DashboardUserId, entry.ExpectedTelegramId);
        else
            cache.Remove(AccountLinkCacheKey(previousCode));

        cache.Remove(UserActiveCodeKey(userId));
    }

    private void InvalidateActiveCodeForTelegram(long telegramId)
    {
        if (!cache.TryGetValue(TelegramActiveCodeKey(telegramId), out string? previousCode)
            || string.IsNullOrWhiteSpace(previousCode))
        {
            return;
        }

        if (cache.TryGetValue(AccountLinkCacheKey(previousCode), out AccountLinkCacheEntry? entry) && entry is not null)
            RemoveCode(previousCode, entry.DashboardUserId, entry.ExpectedTelegramId);
        else
            cache.Remove(AccountLinkCacheKey(previousCode));

        cache.Remove(TelegramActiveCodeKey(telegramId));
    }

    private void RemoveCode(string normalizedCode, int entryDashboardUserId, long entryTelegramId)
    {
        cache.Remove(AccountLinkCacheKey(normalizedCode));

        if (entryDashboardUserId > 0)
            cache.Remove(UserActiveCodeKey(entryDashboardUserId));

        if (entryTelegramId > 0)
            cache.Remove(TelegramActiveCodeKey(entryTelegramId));
    }

    private static string? NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static CompleteTelegramAccountLinkResponse Fail(string message)
        => new() { Success = false, Message = message };

    private static string AccountLinkCacheKey(string code) => $"account-link:code:{code}";

    private static string UserActiveCodeKey(int userId) => $"account-link:user:{userId}";

    private static string TelegramActiveCodeKey(long telegramId) => $"account-link:telegram:{telegramId}";

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[CodeLength];
        RandomNumberGenerator.Fill(bytes);
        var result = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
}
