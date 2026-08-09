namespace DataGateMonitor.Models.EmailTemplates;

/// <summary>Unique <c>EmailBroadcastTemplate.Name</c> for built-in transactional layouts (seeded).</summary>
public static class SystemEmailTemplateNames
{
    public const string EmailConfirmation = "system.email_confirmation";
    public const string AdminPasswordReset = "system.admin_password_reset";
    public const string FreeTierGraceDisconnected = "system.free_tier_grace_disconnected";
    public const string FreeTierChannelSubscribeReminder = "system.free_tier_channel_subscribe_reminder";
}
