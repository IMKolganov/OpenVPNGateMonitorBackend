using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.TelegramBot;

public sealed class TelegramDirectMessageSender(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramChannelSettings> options,
    ILogger<TelegramDirectMessageSender> logger) : ITelegramDirectMessageSender
{
    private const int MaxProfilePhotoBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> TrySendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            logger.LogWarning(
                "Telegram bot token is not configured; cannot send direct message to {ChatId}",
                chatId);
            return false;
        }

        if (chatId <= 0 || string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{settings.BotToken}/sendMessage";
            var client = httpClientFactory.CreateClient();
            using var response = await client.PostAsJsonAsync(
                url,
                new TelegramSendMessageRequest { ChatId = chatId, Text = text },
                ct);

            return await IsOkResponseAsync(response, chatId, "sendMessage", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Telegram direct message to {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> TrySendPhotoAsync(
        long chatId,
        byte[]? photoBytes,
        string caption,
        string fileName = "photo.jpg",
        CancellationToken ct = default)
    {
        if (photoBytes is not { Length: > 0 })
            return await TrySendMessageAsync(chatId, caption, ct);

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            logger.LogWarning(
                "Telegram bot token is not configured; cannot send photo to {ChatId}",
                chatId);
            return false;
        }

        if (chatId <= 0)
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{settings.BotToken}/sendPhoto";
            var client = httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId.ToString()), "chat_id");
            if (!string.IsNullOrWhiteSpace(caption))
                content.Add(new StringContent(caption), "caption");

            var photoContent = new ByteArrayContent(photoBytes);
            photoContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(photoContent, "photo", string.IsNullOrWhiteSpace(fileName) ? "photo.jpg" : fileName);

            using var response = await client.PostAsync(url, content, ct);
            if (await IsOkResponseAsync(response, chatId, "sendPhoto", ct))
                return true;

            logger.LogWarning("Telegram sendPhoto failed for {ChatId}; falling back to text", chatId);
            return await TrySendMessageAsync(chatId, caption, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Telegram photo to {ChatId}; falling back to text", chatId);
            return await TrySendMessageAsync(chatId, caption, ct);
        }
    }

    public async Task<byte[]?> TryDownloadUserProfilePhotoAsync(long telegramUserId, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken) || telegramUserId <= 0)
            return null;

        try
        {
            var client = httpClientFactory.CreateClient();
            var photosUrl =
                $"https://api.telegram.org/bot{settings.BotToken}/getUserProfilePhotos?user_id={telegramUserId}&limit=1";
            using var photosResponse = await client.GetAsync(photosUrl, ct);
            if (!photosResponse.IsSuccessStatusCode)
                return null;

            var photosPayload =
                await photosResponse.Content.ReadFromJsonAsync<TelegramApiResponse<UserProfilePhotosResult>>(
                    JsonOptions, ct);
            if (photosPayload is not { Ok: true, Result: { TotalCount: > 0 } })
                return null;

            var sizes = photosPayload.Result.Photos?.LastOrDefault();
            var biggest = sizes?.LastOrDefault();
            if (biggest is null || string.IsNullOrWhiteSpace(biggest.FileId))
                return null;

            var fileUrl = $"https://api.telegram.org/bot{settings.BotToken}/getFile?file_id={Uri.EscapeDataString(biggest.FileId)}";
            using var fileResponse = await client.GetAsync(fileUrl, ct);
            if (!fileResponse.IsSuccessStatusCode)
                return null;

            var filePayload =
                await fileResponse.Content.ReadFromJsonAsync<TelegramApiResponse<TelegramFileResult>>(JsonOptions, ct);
            if (filePayload is not { Ok: true } || string.IsNullOrWhiteSpace(filePayload.Result?.FilePath))
                return null;

            var downloadUrl = $"https://api.telegram.org/file/bot{settings.BotToken}/{filePayload.Result.FilePath}";
            using var downloadResponse = await client.GetAsync(downloadUrl, ct);
            if (!downloadResponse.IsSuccessStatusCode)
                return null;

            var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > MaxProfilePhotoBytes)
                return null;

            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download Telegram profile photo for {TelegramUserId}", telegramUserId);
            return null;
        }
    }

    private async Task<bool> IsOkResponseAsync(
        HttpResponseMessage response,
        long chatId,
        string method,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Telegram {Method} failed for {ChatId}: HTTP {StatusCode}. {ErrorBody}",
                method,
                chatId,
                (int)response.StatusCode,
                errorBody);
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(JsonOptions, ct);
        if (payload is { Ok: true })
            return true;

        logger.LogWarning(
            "Telegram {Method} returned error for {ChatId}: {Description}",
            method,
            chatId,
            payload?.Description ?? "unknown");
        return false;
    }

    private sealed class TelegramSendMessageRequest
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; init; }

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class TelegramApiResponse
    {
        public bool Ok { get; set; }
        public string? Description { get; set; }
    }

    private sealed class TelegramApiResponse<T>
    {
        public bool Ok { get; set; }
        public string? Description { get; set; }
        public T? Result { get; set; }
    }

    private sealed class UserProfilePhotosResult
    {
        public int TotalCount { get; set; }
        public List<List<TelegramPhotoSize>>? Photos { get; set; }
    }

    private sealed class TelegramPhotoSize
    {
        [JsonPropertyName("file_id")]
        public string? FileId { get; set; }
    }

    private sealed class TelegramFileResult
    {
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }
    }
}
