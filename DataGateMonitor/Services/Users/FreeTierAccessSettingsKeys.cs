namespace DataGateMonitor.Services.Users;

public static class FreeTierAccessSettingsKeys
{
    public const string AllowGraceWithoutCompliance = "FreeTier_Allow_Grace_Without_Compliance";
    public const string GracePeriodMinutes = "FreeTier_Grace_Period_Minutes";
    public const string EnforceOpenVpnSessions = "FreeTier_Enforce_OpenVpn_Sessions";
    public const string EnforcementIntervalMinutes = "FreeTier_Enforcement_Interval_Minutes";
    public const string RevokeOvpnOnEnforcement = "FreeTier_Revoke_Ovpn_On_Enforcement";

    /// <summary>When true, DM Free/Default Telegram users who are not subscribed to the required channel.</summary>
    public const string SendUnsubscribedUserReminders = "FreeTier_Send_Unsubscribed_User_Reminders";

    /// <summary>When true, send bot admins a daily digest of Free/Default VPN users without channel subscription.</summary>
    public const string DailyUnsubscribedAdminDigest = "FreeTier_Daily_Unsubscribed_Admin_Digest";
}
