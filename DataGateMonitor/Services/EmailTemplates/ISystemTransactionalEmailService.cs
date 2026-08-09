namespace DataGateMonitor.Services.EmailTemplates;

public interface ISystemTransactionalEmailService
{
    Task<(string Subject, string BodyHtml)> GetEmailConfirmationAsync(string code, int ttlMinutes, CancellationToken ct);

    Task<(string Subject, string BodyHtml)> GetAdminPasswordResetAsync(string code, int ttlMinutes, CancellationToken ct);

    Task<(string Subject, string BodyHtml)> GetFreeTierGraceDisconnectedAsync(string planName, string requiredChannel, CancellationToken ct);

    Task<(string Subject, string BodyHtml)> GetFreeTierChannelSubscribeReminderAsync(
        string displayName, string requiredChannel, string channelUrl, CancellationToken ct);
}
