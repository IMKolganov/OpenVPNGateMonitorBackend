using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Enums;

namespace DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;

/// <summary>Result of an admin force channel-subscribe reminder (Telegram DM or email).</summary>
public sealed class FreeTierChannelSubscribeRemindResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public FreeTierChannelSubscribeRemindChannel Channel { get; set; }

    public int? UserId { get; set; }

    public long? TelegramId { get; set; }

    public string? Email { get; set; }

    public static FreeTierChannelSubscribeRemindResponse Ok(
        FreeTierChannelSubscribeRemindChannel channel,
        string message,
        int? userId = null,
        long? telegramId = null,
        string? email = null)
        => new()
        {
            Success = true,
            Message = message,
            Channel = channel,
            UserId = userId,
            TelegramId = telegramId,
            Email = email,
        };

    public static FreeTierChannelSubscribeRemindResponse Fail(
        FreeTierChannelSubscribeRemindChannel channel,
        string message)
        => new()
        {
            Success = false,
            Message = message,
            Channel = channel,
        };
}
