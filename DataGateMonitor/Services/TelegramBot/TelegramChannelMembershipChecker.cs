using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.TelegramBot.Interfaces;

namespace DataGateMonitor.Services.TelegramBot;

public sealed class TelegramChannelMembershipChecker(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramChannelSettings> options,
    ILogger<TelegramChannelMembershipChecker> logger) : ITelegramChannelMembershipChecker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> IsSubscribedAsync(long telegramUserId, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            logger.LogWarning(
                "Telegram bot token is not configured; cannot verify channel subscription for user {TelegramUserId}",
                telegramUserId);
            return false;
        }

        if (telegramUserId <= 0)
            return false;

        var chatId = settings.RequiredChannelChatId;
        var url =
            $"https://api.telegram.org/bot{settings.BotToken}/getChatMember?chat_id={Uri.EscapeDataString(chatId)}&user_id={telegramUserId}";

        var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var telegramDescription = TryReadTelegramDescription(errorBody) ?? errorBody;
            LogGetChatMemberFailure(telegramUserId, chatId, (int)response.StatusCode, telegramDescription);
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<TelegramApiResponse<TelegramChatMemberPayload>>(JsonOptions, ct);
        if (payload is not { Ok: true, Result: not null })
        {
            LogGetChatMemberFailure(telegramUserId, chatId, statusCode: null, payload?.Description ?? "unknown");
            return false;
        }

        return IsActiveMemberStatus(payload.Result.Status);
    }

    internal static bool IsActiveMemberStatus(string? status)
        => status?.ToLowerInvariant() switch
        {
            "creator" or "administrator" or "member" or "restricted" => true,
            _ => false,
        };

    /// <summary>
    /// Telegram often returns these instead of a chat-member status when the user is unknown to the bot,
    /// never joined, left, or deactivated. Treat as not subscribed — not as an operational incident.
    /// </summary>
    internal static bool IsExpectedNonMembershipDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var text = description.ToLowerInvariant();
        return text.Contains("participant_id_invalid", StringComparison.Ordinal)
               || text.Contains("user not found", StringComparison.Ordinal)
               || text.Contains("member not found", StringComparison.Ordinal)
               || text.Contains("user is deactivated", StringComparison.Ordinal)
               || text.Contains("user_deactivated", StringComparison.Ordinal)
               || text.Contains("peer_id_invalid", StringComparison.Ordinal);
    }

    private void LogGetChatMemberFailure(long telegramUserId, string chatId, int? statusCode, string description)
    {
        if (IsExpectedNonMembershipDescription(description))
        {
            if (statusCode is int code)
            {
                logger.LogDebug(
                    "Telegram getChatMember: user {TelegramUserId} not a member of {ChatId} (HTTP {StatusCode}): {TelegramDescription}",
                    telegramUserId,
                    chatId,
                    code,
                    description);
            }
            else
            {
                logger.LogDebug(
                    "Telegram getChatMember: user {TelegramUserId} not a member of {ChatId}: {TelegramDescription}",
                    telegramUserId,
                    chatId,
                    description);
            }

            return;
        }

        if (statusCode is int httpCode)
        {
            logger.LogWarning(
                "Telegram getChatMember failed for user {TelegramUserId} in {ChatId}: HTTP {StatusCode}. Telegram: {TelegramDescription}",
                telegramUserId,
                chatId,
                httpCode,
                description);
        }
        else
        {
            logger.LogWarning(
                "Telegram getChatMember returned error for user {TelegramUserId} in {ChatId}: {Description}",
                telegramUserId,
                chatId,
                description);
        }
    }

    private static string? TryReadTelegramDescription(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("description", out var description))
                return description.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed class TelegramApiResponse<T>
    {
        public bool Ok { get; set; }
        public string? Description { get; set; }
        public T? Result { get; set; }
    }

    private sealed class TelegramChatMemberPayload
    {
        public string? Status { get; set; }
    }
}
