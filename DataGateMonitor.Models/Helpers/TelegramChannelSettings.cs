namespace DataGateMonitor.Models.Helpers;

public sealed class TelegramChannelSettings
{
    public const string SectionName = "TelegramChannel";

    /// <summary>Required public channel username without @ (e.g. DataGateVPNBot).</summary>
    public string RequiredChannelUsername { get; set; } = "DataGateVPNBot";

    /// <summary>Public bot username without @ (for deep links like t.me/Bot?start=…).</summary>
    public string BotUsername { get; set; } = "DataGateVPNBot";

    /// <summary>Bot token used for getChatMember when channel subscription is verified from the backend.</summary>
    public string? BotToken { get; set; }

    public static void ApplyEnvOverrides(TelegramChannelSettings settings)
    {
        var envToken = Environment.GetEnvironmentVariable("TELEGRAMBOT_BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            settings.BotToken = envToken;

        var envChannel = Environment.GetEnvironmentVariable("TELEGRAM_REQUIRED_CHANNEL_USERNAME");
        if (!string.IsNullOrWhiteSpace(envChannel))
            settings.RequiredChannelUsername = envChannel.Trim().TrimStart('@');

        var envBot = Environment.GetEnvironmentVariable("TELEGRAM_BOT_USERNAME");
        if (!string.IsNullOrWhiteSpace(envBot))
            settings.BotUsername = envBot.Trim().TrimStart('@');
    }

    public string RequiredChannelChatId =>
        $"@{RequiredChannelUsername.Trim().TrimStart('@')}";

    public string RequiredChannelUrl =>
        $"https://t.me/{RequiredChannelUsername.Trim().TrimStart('@')}";

    public string BotUrl =>
        $"https://t.me/{BotUsername.Trim().TrimStart('@')}";

    /// <summary>Deep link that opens the bot with <c>/start link_CODE</c> so account linking runs automatically.</summary>
    public string BuildAccountLinkDeepLink(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return $"{BotUrl}?start=link_{normalized}";
    }
}
