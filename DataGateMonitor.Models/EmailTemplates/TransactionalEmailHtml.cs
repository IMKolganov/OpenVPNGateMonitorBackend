using System.Globalization;
using System.Net;
using System.Text;

namespace DataGateMonitor.Models.EmailTemplates;

/// <summary>
/// Unified transactional email layout (light/dark, GitHub-like). Used for registration confirmation,
/// admin password reset, and seeded <c>EmailBroadcastTemplate</c> rows.
/// </summary>
public static class TransactionalEmailHtml
{
    public const string DefaultConfirmationSubject = "Confirm your email — DataGate";
    public const string DefaultAdminPasswordResetSubject = "Administrator password reset — DataGate";
    public const string DefaultFreeTierGraceDisconnectedSubject = "You were disconnected from the VPN — DataGate";
    public const string DefaultFreeTierChannelSubscribeReminderSubject = "Please subscribe to our Telegram channel — DataGate";
    public const string DefaultConfirmEmailPageUrl = "https://datagateapp.com/confirm-email";

    private const string MailVersionLabel = "1.0.3";

    private static string Escape(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string BuildEmailConfirmation(string code, int ttlMinutes)
        => BuildDocument(
            pageTitle: "DataGate — email confirmation",
            emailTitle: "Email confirmation",
            emailTagline: "Desktop client and web dashboard for your VPN.",
            includeDownloadButton: true,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                "Thank you for signing up with DataGate. To finish registration, enter the confirmation code on the website.",
                $"The code is valid for <strong>{Escape(ttlMinutes.ToString(CultureInfo.InvariantCulture))}</strong> minutes. If you did not create an account, you can ignore this message.",
                "Follow our Telegram channel: <a href=\"https://t.me/datagateapp\" target=\"_blank\" rel=\"noopener noreferrer\">https://t.me/datagateapp</a>",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Confirmation code",
            codeValueHtml: Escape(code),
            signoff: "— The DataGate team",
            actionButtonHref: DefaultConfirmEmailPageUrl,
            actionButtonLabel: "Enter confirmation code");

    public static string BuildEmailConfirmationWithPlaceholders()
        => BuildDocument(
            pageTitle: "DataGate — email confirmation",
            emailTitle: "Email confirmation",
            emailTagline: "Desktop client and web dashboard for your VPN.",
            includeDownloadButton: true,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                "Thank you for signing up with DataGate. To finish registration, enter the confirmation code on the website.",
                "The code is valid for <strong>{{TTL_MINUTES}}</strong> minutes. If you did not create an account, you can ignore this message.",
                "Follow our Telegram channel: <a href=\"https://t.me/datagateapp\" target=\"_blank\" rel=\"noopener noreferrer\">https://t.me/datagateapp</a>",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Confirmation code",
            codeValueHtml: "{{CODE}}",
            signoff: "— The DataGate team",
            actionButtonHref: DefaultConfirmEmailPageUrl,
            actionButtonLabel: "Enter confirmation code");

    public static string BuildAdminPasswordReset(string code, int ttlMinutes)
        => BuildDocument(
            pageTitle: "DataGate — password reset",
            emailTitle: "Administrator password reset",
            emailTagline: "One-time code for the password recovery form.",
            includeDownloadButton: true,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                "You requested a password reset for the DataGate dashboard. Enter the code below in the “Forgot password” / “New password” form.",
                $"The code is valid for <strong>{Escape(ttlMinutes.ToString(CultureInfo.InvariantCulture))}</strong> minutes. If this was not you, change your password after signing in or contact other administrators.",
                "Follow our Telegram channel: <a href=\"https://t.me/datagateapp\" target=\"_blank\" rel=\"noopener noreferrer\">https://t.me/datagateapp</a>",
            ],
            codeLabel: "Reset code",
            codeValueHtml: Escape(code),
            signoff: "— The DataGate team");

    public static string BuildAdminPasswordResetWithPlaceholders()
        => BuildDocument(
            pageTitle: "DataGate — password reset",
            emailTitle: "Administrator password reset",
            emailTagline: "One-time code for the password recovery form.",
            includeDownloadButton: true,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                "You requested a password reset for the DataGate dashboard. Enter the code below in the “Forgot password” / “New password” form.",
                "The code is valid for <strong>{{TTL_MINUTES}}</strong> minutes. If this was not you, change your password after signing in or contact other administrators.",
                "Follow our Telegram channel: <a href=\"https://t.me/datagateapp\" target=\"_blank\" rel=\"noopener noreferrer\">https://t.me/datagateapp</a>",
            ],
            codeLabel: "Reset code",
            codeValueHtml: "{{CODE}}",
            signoff: "— The DataGate team");

    public static string BuildFreeTierGraceDisconnected(string planName, string requiredChannel)
        => BuildDocument(
            pageTitle: "DataGate — disconnected after grace period",
            emailTitle: "You were disconnected",
            emailTagline: "Your Free/Default plan requires channel subscription or a linked account.",
            includeDownloadButton: false,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                $"You connected to the VPN and were allowed a short grace period, but you were disconnected because your account (plan <strong>{Escape(planName)}</strong>) still isn't subscribed to the required Telegram channel and isn't a linked (merged) account.",
                $"Subscribe to <strong>{Escape(requiredChannel)}</strong> or link your Telegram account to your Google/local account in the app, then reconnect.",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Required channel",
            codeValueHtml: Escape(requiredChannel),
            signoff: "— The DataGate team");

    public static string BuildFreeTierGraceDisconnectedWithPlaceholders()
        => BuildDocument(
            pageTitle: "DataGate — disconnected after grace period",
            emailTitle: "You were disconnected",
            emailTagline: "Your Free/Default plan requires channel subscription or a linked account.",
            includeDownloadButton: false,
            emailLead: "Hello,",
            bodyParagraphs:
            [
                "You connected to the VPN and were allowed a short grace period, but you were disconnected because your account (plan <strong>{{PLAN_NAME}}</strong>) still isn't subscribed to the required Telegram channel and isn't a linked (merged) account.",
                "Subscribe to <strong>{{REQUIRED_CHANNEL}}</strong> or link your Telegram account to your Google/local account in the app, then reconnect.",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Required channel",
            codeValueHtml: "{{REQUIRED_CHANNEL}}",
            signoff: "— The DataGate team");

    public static string ApplyFreeTierGraceDisconnectedPlaceholders(string bodyHtml, string planName, string requiredChannel)
        => bodyHtml
            .Replace("{{PLAN_NAME}}", Escape(planName), StringComparison.Ordinal)
            .Replace("{{REQUIRED_CHANNEL}}", Escape(requiredChannel), StringComparison.Ordinal);

    public static string BuildFreeTierChannelSubscribeReminder(string displayName, string requiredChannel, string channelUrl)
        => BuildDocument(
            pageTitle: "DataGate — subscribe to Telegram channel",
            emailTitle: "Please subscribe to our channel",
            emailTagline: "Free/Default VPN access requires an active Telegram channel subscription.",
            includeDownloadButton: false,
            emailLead: string.IsNullOrWhiteSpace(displayName) ? "Hello," : $"Hello {Escape(displayName)},",
            bodyParagraphs:
            [
                "To keep using Free/Default VPN access, please subscribe to our official Telegram channel.",
                $"Channel: <strong>{Escape(requiredChannel)}</strong><br/><a href=\"{Escape(channelUrl)}\" target=\"_blank\" rel=\"noopener noreferrer\">{Escape(channelUrl)}</a>",
                "After you subscribe, reconnect to the VPN if you were disconnected.",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Required channel",
            codeValueHtml: Escape(requiredChannel),
            signoff: "— The DataGate team",
            actionButtonHref: channelUrl,
            actionButtonLabel: "Open Telegram channel");

    public static string BuildFreeTierChannelSubscribeReminderWithPlaceholders()
        => BuildDocument(
            pageTitle: "DataGate — subscribe to Telegram channel",
            emailTitle: "Please subscribe to our channel",
            emailTagline: "Free/Default VPN access requires an active Telegram channel subscription.",
            includeDownloadButton: false,
            emailLead: "Hello {{DISPLAY_NAME}},",
            bodyParagraphs:
            [
                "To keep using Free/Default VPN access, please subscribe to our official Telegram channel.",
                "Channel: <strong>{{REQUIRED_CHANNEL}}</strong><br/><a href=\"{{CHANNEL_URL}}\" target=\"_blank\" rel=\"noopener noreferrer\">{{CHANNEL_URL}}</a>",
                "After you subscribe, reconnect to the VPN if you were disconnected.",
                "If you need help: <a href=\"https://t.me/KolganovIvan\" target=\"_blank\" rel=\"noopener noreferrer\"><b>@KolganovIvan</b></a>",
            ],
            codeLabel: "Required channel",
            codeValueHtml: "{{REQUIRED_CHANNEL}}",
            signoff: "— The DataGate team",
            actionButtonHref: "{{CHANNEL_URL}}",
            actionButtonLabel: "Open Telegram channel");

    public static string ApplyFreeTierChannelSubscribeReminderPlaceholders(
        string bodyHtml, string displayName, string requiredChannel, string channelUrl)
        => bodyHtml
            .Replace("{{DISPLAY_NAME}}", Escape(displayName), StringComparison.Ordinal)
            .Replace("{{REQUIRED_CHANNEL}}", Escape(requiredChannel), StringComparison.Ordinal)
            .Replace("{{CHANNEL_URL}}", Escape(channelUrl), StringComparison.Ordinal);

    public static string ApplyConfirmationPlaceholders(string bodyHtml, string code, int ttlMinutes)
    {
        return bodyHtml
            .Replace("{{CODE}}", Escape(code), StringComparison.Ordinal)
            .Replace("{{TTL_MINUTES}}", ttlMinutes.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    public static string ApplyPasswordResetPlaceholders(string bodyHtml, string code, int ttlMinutes)
        => ApplyConfirmationPlaceholders(bodyHtml, code, ttlMinutes);

    /// <summary>
    /// Ensures seeded/custom confirmation templates include a link to the web confirmation form.
    /// </summary>
    public static string EnsureConfirmEmailAction(string bodyHtml)
    {
        if (string.IsNullOrEmpty(bodyHtml))
            return bodyHtml;

        if (bodyHtml.Contains(DefaultConfirmEmailPageUrl, StringComparison.OrdinalIgnoreCase))
            return bodyHtml;

        const string marker = "<div class=\"code-panel\"";
        var index = bodyHtml.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return bodyHtml;

        var button =
            $"""
            <div class="btn-wrap">
              <a class="btn" href="{Escape(DefaultConfirmEmailPageUrl)}" target="_blank" rel="noopener noreferrer">
                Enter confirmation code
              </a>
            </div>

            <br />

            """;

        return bodyHtml.Insert(index, button);
    }

    private static string BuildDocument(
        string pageTitle,
        string emailTitle,
        string emailTagline,
        bool includeDownloadButton,
        string emailLead,
        IReadOnlyList<string> bodyParagraphs,
        string codeLabel,
        string codeValueHtml,
        string signoff,
        string? actionButtonHref = null,
        string? actionButtonLabel = null)
    {
        var sb = new StringBuilder();
        sb.Append(CssAndHeadOpen(pageTitle));
        sb.Append("<body>\n");
        sb.Append(
            """
            <table role="presentation" class="wrap" width="100%" cellpadding="0" cellspacing="0" border="0">
              <tr>
                <td align="center" style="padding:24px 12px;">
                  <table role="presentation" class="shell" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;">
                    <tr>
                      <td>
                        <table role="presentation" class="card" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#ffffff;border:1px solid #d0d7de;border-radius:12px;">
                          <tr>
                            <td class="accent">&nbsp;</td>
                          </tr>
                          <tr>
                            <td class="pad">
            """);

        sb.Append($"                    <p class=\"email-title\">{Escape(emailTitle)}</p>\n");
        sb.Append($"                    <p class=\"email-tagline\">{Escape(emailTagline)}</p>\n");

        if (includeDownloadButton)
        {
            sb.Append(
                """
                                <div class="btn-wrap">
                                  <a class="btn" href="https://datagateapp.com/download" target="_blank" rel="noopener noreferrer">
                                    Download the latest DataGate
                                  </a>
                                </div>

                                <br />

                """);
        }

        sb.Append($"                    <p class=\"email-lead\">{Escape(emailLead)}</p>\n");

        foreach (var p in bodyParagraphs)
            sb.Append($"                    <p class=\"email-body\">{p}</p>\n");

        if (!string.IsNullOrWhiteSpace(actionButtonHref) && !string.IsNullOrWhiteSpace(actionButtonLabel))
        {
            sb.Append(
                $"""
                            <div class="btn-wrap">
                              <a class="btn" href="{Escape(actionButtonHref)}" target="_blank" rel="noopener noreferrer">
                                {Escape(actionButtonLabel)}
                              </a>
                            </div>

                            <br />

                """);
        }

        sb.Append(
            $"""
                            <div class="code-panel" style="margin:18px 0 12px;padding:14px 16px;border:1px solid #d0d7de;border-radius:10px;text-align:center;background:#f6f8fa;">
                              <div style="font-size:12px;color:#656d76;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:8px;">{Escape(codeLabel)}</div>
                              <div style="font-size:26px;font-weight:700;letter-spacing:0.18em;font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,monospace;color:#24292f;">{codeValueHtml}</div>
                            </div>

            """);

        sb.Append($"                    <p class=\"email-signoff\">{Escape(signoff)}</p>\n");

        sb.Append(
            """
                            </td>
                          </tr>
                          <tr>
                            <td class="footer">
                              <div class="footer-muted">
                                Open clients and server-side tools for full control of your VPN
                              </div>
                              <div class="footer-muted">
                                <a href="https://datagateapp.com/" target="_blank" rel="noopener noreferrer">datagateapp.com</a>
                              </div>
                              <div>© 2026 DataGate v.
            """);

        sb.Append(MailVersionLabel);
        sb.Append(
            """
            </div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string CssAndHeadOpen(string pageTitle)
    {
        var t = Escape(pageTitle);
        return """
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="color-scheme" content="light dark" />
              <meta name="supported-color-schemes" content="light dark" />
              <title>__TITLE__</title>
              <style type="text/css">
                html, body {
                  margin: 0 !important;
                  padding: 0 !important;
                  width: 100% !important;
                  -webkit-text-size-adjust: 100%;
                  -ms-text-size-adjust: 100%;
                }

                body {
                  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
                  font-size: 16px;
                  line-height: 1.55;
                  color: #1f2328;
                  background-color: #f6f8fa;
                }

                .wrap { width: 100%; background-color: #f6f8fa; }
                .shell { max-width: 600px; margin: 0 auto; }

                .card {
                  background-color: #ffffff;
                  border: 1px solid #d0d7de;
                  border-radius: 12px;
                  overflow: hidden;
                }

                .accent {
                  height: 4px;
                  line-height: 0;
                  font-size: 0;
                  background: linear-gradient(90deg, #238636, #2ea043);
                }

                .pad { padding: 28px 24px 24px; }

                .email-title {
                  margin: 0 0 4px;
                  font-size: 22px;
                  font-weight: 600;
                  color: #1f2328;
                  letter-spacing: -0.02em;
                }

                .email-tagline {
                  margin: 0 0 24px;
                  font-size: 13px;
                  color: #656d76;
                }

                .email-lead {
                  margin: 0 0 16px;
                  font-size: 16px;
                  font-weight: 500;
                  color: #24292f;
                }

                .email-body {
                  margin: 0 0 16px;
                  font-size: 15px;
                  color: #424a53;
                  line-height: 1.55;
                }

                .email-body:last-of-type { margin-bottom: 0; }

                .email-body a {
                  color: #0969da;
                  text-decoration: none;
                  font-weight: 500;
                }

                .email-body a:hover { text-decoration: underline; }

                .email-signoff {
                  margin: 0;
                  font-size: 15px;
                  color: #424a53;
                  line-height: 1.55;
                }

                .btn-wrap { margin-top: 16px; margin-bottom: 8px; }

                .btn {
                  display: inline-block;
                  padding: 10px 18px;
                  font-size: 14px;
                  font-weight: 600;
                  text-decoration: none;
                  border-radius: 8px;
                  background-color: #238636;
                  color: #ffffff !important;
                  border: 1px solid #2ea043;
                }

                .footer {
                  padding: 20px 24px 28px;
                  font-size: 12px;
                  line-height: 1.5;
                  color: #656d76;
                  text-align: center;
                  border-top: 1px solid #d0d7de;
                  background-color: #f6f8fa;
                }

                .footer a { color: #0969da; text-decoration: none; font-weight: 500; }

                .footer-muted { margin-bottom: 8px; }

                @media (prefers-color-scheme: dark) {
                  body {
                    color: #e6edf3 !important;
                    background-color: #0d1117 !important;
                  }
                  .wrap { background-color: #0d1117 !important; }
                  .card {
                    background-color: #161b22 !important;
                    border-color: #30363d !important;
                  }
                  .email-title { color: #f0f6fc !important; }
                  .email-tagline { color: #9da7b3 !important; }
                  .email-lead { color: #e6edf3 !important; }
                  .email-body,
                  .email-signoff {
                    color: #c9d1d9 !important;
                  }
                  .email-body a { color: #79c0ff !important; }
                  .code-panel {
                    background-color: #21262d !important;
                    border-color: #30363d !important;
                  }
                  .code-panel div:last-child { color: #f0f6fc !important; }
                  .footer {
                    color: #9da7b3 !important;
                    border-top-color: #30363d !important;
                    background-color: #0d1117 !important;
                  }
                  .footer a { color: #79c0ff !important; }
                }

                @media only screen and (max-width: 620px) {
                  .pad { padding: 22px 18px 18px !important; }
                  .email-title { font-size: 20px !important; }
                  .btn { display: block !important; text-align: center !important; }
                }
              </style>
            </head>
            """.Replace("__TITLE__", t, StringComparison.Ordinal);
    }
}
