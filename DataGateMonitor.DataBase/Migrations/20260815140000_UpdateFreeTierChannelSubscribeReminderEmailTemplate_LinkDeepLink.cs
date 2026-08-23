using DataGateMonitor.Models.EmailTemplates;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFreeTierChannelSubscribeReminderEmailTemplate_LinkDeepLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var epoch = new DateTimeOffset(2026, 8, 15, 14, 0, 0, TimeSpan.Zero);
            var description =
                "Built-in: admin email remind — subscribe to channel and/or link Google↔Telegram. Placeholders: {{DISPLAY_NAME}}, {{REQUIRED_CHANNEL}}, {{CHANNEL_URL}}, {{LINK_CODE}}, {{LINK_TTL_MINUTES}}, {{CODE_LABEL}}, {{CODE_VALUE}}, {{ACTION_URL}}, {{ACTION_LABEL}}; optional block <!--BEGIN_LINK_ACCOUNT-->…<!--END_LINK_ACCOUNT-->";
            var subject = TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject;
            var body = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();

            migrationBuilder.Sql(
                $"""
                UPDATE xgb_dashopnvpn."EmailBroadcastTemplates"
                SET
                    "Description" = {ToDollarQuoted(description)},
                    "Subject" = {ToDollarQuoted(subject)},
                    "BodyHtml" = {ToDollarQuoted(body)},
                    "LastUpdate" = TIMESTAMPTZ '{epoch:yyyy-MM-dd HH:mm:ss}+00'
                WHERE "Name" = 'system.free_tier_channel_subscribe_reminder';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Previous template is superseded; code fallback still builds a valid email.
        }

        private static string ToDollarQuoted(string value)
        {
            // Avoid colliding with the chosen dollar-quote tag inside the payload.
            const string tag = "mightml";
            var safe = value.Replace("$" + tag + "$", "$ " + tag + " $", StringComparison.Ordinal);
            return "$" + tag + "$" + safe + "$" + tag + "$";
        }
    }
}
