using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.Models;
using DataGateMonitor.Models.EmailTemplates;

namespace DataGateMonitor.Services.EmailTemplates;

public sealed class SystemTransactionalEmailService(IQueryService<EmailBroadcastTemplate, int> templateQuery)
    : ISystemTransactionalEmailService
{
    public async Task<(string Subject, string BodyHtml)> GetEmailConfirmationAsync(string code, int ttlMinutes,
        CancellationToken ct)
    {
        var entity = await templateQuery.FirstOrDefault(
            t => t.Name == SystemEmailTemplateNames.EmailConfirmation,
            orderBy: q => q.OrderBy(t => t.Id),
            asNoTracking: true,
            ct: ct);

        if (entity is { BodyHtml: { Length: > 0 } body })
        {
            var subject = string.IsNullOrWhiteSpace(entity.Subject)
                ? TransactionalEmailHtml.DefaultConfirmationSubject
                : entity.Subject.Trim();
            return (subject, TransactionalEmailHtml.ApplyConfirmationPlaceholders(
                TransactionalEmailHtml.EnsureConfirmEmailAction(body), code, ttlMinutes));
        }

        return (TransactionalEmailHtml.DefaultConfirmationSubject, TransactionalEmailHtml.BuildEmailConfirmation(code, ttlMinutes));
    }

    public async Task<(string Subject, string BodyHtml)> GetAdminPasswordResetAsync(string code, int ttlMinutes,
        CancellationToken ct)
    {
        var entity = await templateQuery.FirstOrDefault(
            t => t.Name == SystemEmailTemplateNames.AdminPasswordReset,
            orderBy: q => q.OrderBy(t => t.Id),
            asNoTracking: true,
            ct: ct);

        if (entity is { BodyHtml: { Length: > 0 } body })
        {
            var subject = string.IsNullOrWhiteSpace(entity.Subject)
                ? TransactionalEmailHtml.DefaultAdminPasswordResetSubject
                : entity.Subject.Trim();
            return (subject, TransactionalEmailHtml.ApplyPasswordResetPlaceholders(body, code, ttlMinutes));
        }

        return (TransactionalEmailHtml.DefaultAdminPasswordResetSubject,
            TransactionalEmailHtml.BuildAdminPasswordReset(code, ttlMinutes));
    }

    public async Task<(string Subject, string BodyHtml)> GetFreeTierGraceDisconnectedAsync(
        string planName, string requiredChannel, CancellationToken ct)
    {
        var entity = await templateQuery.FirstOrDefault(
            t => t.Name == SystemEmailTemplateNames.FreeTierGraceDisconnected,
            orderBy: q => q.OrderBy(t => t.Id),
            asNoTracking: true,
            ct: ct);

        if (entity is { BodyHtml: { Length: > 0 } body })
        {
            var subject = string.IsNullOrWhiteSpace(entity.Subject)
                ? TransactionalEmailHtml.DefaultFreeTierGraceDisconnectedSubject
                : entity.Subject.Trim();
            return (subject, TransactionalEmailHtml.ApplyFreeTierGraceDisconnectedPlaceholders(body, planName, requiredChannel));
        }

        return (TransactionalEmailHtml.DefaultFreeTierGraceDisconnectedSubject,
            TransactionalEmailHtml.BuildFreeTierGraceDisconnected(planName, requiredChannel));
    }

    public async Task<(string Subject, string BodyHtml)> GetFreeTierChannelSubscribeReminderAsync(
        string displayName,
        string requiredChannel,
        string channelUrl,
        string? linkCode,
        string? linkBotUrl,
        int linkCodeTtlMinutes,
        CancellationToken ct)
    {
        var entity = await templateQuery.FirstOrDefault(
            t => t.Name == SystemEmailTemplateNames.FreeTierChannelSubscribeReminder,
            orderBy: q => q.OrderBy(t => t.Id),
            asNoTracking: true,
            ct: ct);

        // Stale DB seeds only replace {{DISPLAY_NAME}}/channel tokens and never include the
        // account-link deep link. Prefer the code builder unless the template is modern.
        if (entity is { BodyHtml: { Length: > 0 } body } &&
            IsModernFreeTierChannelSubscribeReminderTemplate(body))
        {
            var subject = string.IsNullOrWhiteSpace(entity.Subject)
                ? TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject
                : entity.Subject.Trim();
            return (subject, TransactionalEmailHtml.ApplyFreeTierChannelSubscribeReminderPlaceholders(
                body, displayName, requiredChannel, channelUrl, linkCode, linkBotUrl, linkCodeTtlMinutes));
        }

        return (TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject,
            TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminder(
                displayName, requiredChannel, channelUrl, linkCode, linkBotUrl, linkCodeTtlMinutes));
    }

    /// <summary>
    /// Modern templates must support deep-link account linking placeholders.
    /// </summary>
    internal static bool IsModernFreeTierChannelSubscribeReminderTemplate(string bodyHtml)
        => bodyHtml.Contains("{{ACTION_URL}}", StringComparison.Ordinal)
           && bodyHtml.Contains("<!--BEGIN_LINK_ACCOUNT-->", StringComparison.Ordinal);
}
