using DataGateMonitor.Models.EmailTemplates;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class SeedFreeTierChannelSubscribeReminderEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var epoch = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "EmailBroadcastTemplates",
                columns: ["Name", "Description", "Subject", "BodyHtml", "CreatedByUserId", "CreateDate", "LastUpdate"],
                values: new object[,]
                {
                    {
                        SystemEmailTemplateNames.FreeTierChannelSubscribeReminder,
                        "Built-in: admin email remind — subscribe to channel and/or link Google↔Telegram. Placeholders: {{DISPLAY_NAME}}, {{REQUIRED_CHANNEL}}, {{CHANNEL_URL}}, {{LINK_CODE}}, {{LINK_TTL_MINUTES}}, {{CODE_LABEL}}, {{CODE_VALUE}}, {{ACTION_URL}}, {{ACTION_LABEL}}; optional block <!--BEGIN_LINK_ACCOUNT-->…<!--END_LINK_ACCOUNT-->",
                        TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject,
                        TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders(),
                        null,
                        epoch,
                        epoch
                    }
                });
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
    }
}
