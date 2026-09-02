using DataGateMonitor.DataBase.Contexts;
using DataGateMonitor.Models.EmailTemplates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815143000_EnsureFreeTierChannelSubscribeReminderEmailTemplate")]
    /// <remarks>
    /// Prod may be missing the Aug 9 seed row; a later UPDATE-only migration then matched 0 rows.
    /// Upsert so deploy creates or refreshes <c>system.free_tier_channel_subscribe_reminder</c>.
    /// </remarks>
    public partial class EnsureFreeTierChannelSubscribeReminderEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var epoch = new DateTimeOffset(2026, 8, 15, 14, 30, 0, TimeSpan.Zero);
            var description =
                "Built-in free-tier remind: channel subscribe + Telegram account link deep link. " +
                "Placeholders: {{DISPLAY_NAME}}, {{REQUIRED_CHANNEL}}, {{CHANNEL_URL}}, {{LINK_CODE}}, " +
                "{{LINK_TTL_MINUTES}}, {{CODE_LABEL}}, {{CODE_VALUE}}, {{ACTION_URL}}, {{ACTION_LABEL}}; " +
                "optional <!--BEGIN_LINK_ACCOUNT--> block.";
            var subject = TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject;
            var body = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();

            migrationBuilder.Sql(
                $"""
                INSERT INTO xgb_dashopnvpn."EmailBroadcastTemplates"
                  ("Name", "Description", "Subject", "BodyHtml", "CreatedByUserId", "CreateDate", "LastUpdate")
                VALUES (
                  'system.free_tier_channel_subscribe_reminder',
                  {ToDollarQuoted(description)},
                  {ToDollarQuoted(subject)},
                  {ToDollarQuoted(body)},
                  NULL,
                  TIMESTAMPTZ '{epoch:yyyy-MM-dd HH:mm:ss}+00',
                  TIMESTAMPTZ '{epoch:yyyy-MM-dd HH:mm:ss}+00'
                )
                ON CONFLICT ("Name") DO UPDATE SET
                  "Description" = EXCLUDED."Description",
                  "Subject" = EXCLUDED."Subject",
                  "BodyHtml" = EXCLUDED."BodyHtml",
                  "LastUpdate" = TIMESTAMPTZ '{epoch:yyyy-MM-dd HH:mm:ss}+00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM xgb_dashopnvpn."EmailBroadcastTemplates"
                WHERE "Name" = 'system.free_tier_channel_subscribe_reminder';
                """);
        }

        private static string ToDollarQuoted(string value)
        {
            const string tag = "mightml";
            var safe = value.Replace("$" + tag + "$", "$ " + tag + " $", StringComparison.Ordinal);
            return "$" + tag + "$" + safe + "$" + tag + "$";
        }
    }
}
