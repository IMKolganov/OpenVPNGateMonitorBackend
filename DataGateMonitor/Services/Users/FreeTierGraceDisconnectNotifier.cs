using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.AdminEmail;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.EmailTemplates;
using DataGateMonitor.Services.TelegramBot.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.Users;

public sealed class FreeTierGraceDisconnectNotifier(
    IUserQueryService userQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IFreeTierAccessComplianceService freeTierAccessComplianceService,
    ITelegramDirectMessageSender telegramDirectMessageSender,
    IEmailSenderService emailSenderService,
    ISystemTransactionalEmailService systemTransactionalEmailService,
    ISentEmailLogService sentEmailLogService,
    IOptions<TelegramChannelSettings> channelOptions,
    ILogger<FreeTierGraceDisconnectNotifier> logger) : IFreeTierGraceDisconnectNotifier
{
    private const string TelegramChannel = "telegram";
    private const string EmailChannel = "email";

    public async Task<FreeTierGraceDisconnectOutcome> NotifyAsync(
        int userId, string? planNameHint = null, CancellationToken ct = default)
    {
        try
        {
            var user = await userQueryService.GetById(userId, ct);
            if (user is null)
                return FreeTierGraceDisconnectOutcome.NoChannelAvailable;

            var links = await userIdentityLinkQueryService.GetListByUserId(userId, ct);
            var telegramId = FreeTierAccessComplianceService.TryGetTelegramId(links);
            var requiredChannel = channelOptions.Value.RequiredChannelChatId;

            var telegramAttempted = false;
            if (telegramId is > 0)
            {
                telegramAttempted = true;
                var text = BuildTelegramMessage(requiredChannel);
                if (await telegramDirectMessageSender.TrySendMessageAsync(telegramId.Value, text, ct))
                    return new FreeTierGraceDisconnectOutcome(TelegramChannel, Sent: true);
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                logger.LogInformation(
                    "No Telegram or email channel available to notify user {UserId} about a free-tier grace-period disconnect.",
                    userId);
                return telegramAttempted
                    ? new FreeTierGraceDisconnectOutcome(TelegramChannel, Sent: false)
                    : FreeTierGraceDisconnectOutcome.NoChannelAvailable;
            }

            var planName = planNameHint;
            if (string.IsNullOrWhiteSpace(planName))
            {
                var evaluation = await freeTierAccessComplianceService.EvaluateAccessForEnforcementAsync(userId, ct);
                planName = evaluation.ActivePlanName;
            }

            planName ??= QuotaPlanNames.Free;

            var (subject, bodyHtml) = await systemTransactionalEmailService.GetFreeTierGraceDisconnectedAsync(
                planName, requiredChannel, ct);

            // Send and audit-log independently: a failure writing the SentEmailLogs row must never be
            // mistaken for (and reported as) an email delivery failure.
            string? sendError = null;
            try
            {
                await emailSenderService.SendAsync(user.Email, subject, bodyHtml, ct);
            }
            catch (Exception ex)
            {
                sendError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                logger.LogWarning(ex, "Failed to send free-tier grace-disconnect email to user {UserId}.", userId);
            }

            try
            {
                await sentEmailLogService.LogAsync(
                    userId, user.Email, subject, bodyHtml, sendError is null, sendError, null, ct);
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write SentEmailLog entry for user {UserId}.", userId);
            }

            return new FreeTierGraceDisconnectOutcome(EmailChannel, Sent: sendError is null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify user {UserId} about a free-tier grace-period disconnect.", userId);
            return FreeTierGraceDisconnectOutcome.NoChannelAvailable;
        }
    }

    private static string BuildTelegramMessage(string requiredChannel)
        => "⚠️ You were disconnected from the VPN. Your grace period ended and you still are not " +
           $"subscribed to {requiredChannel}. Please subscribe, then reconnect.";
}
