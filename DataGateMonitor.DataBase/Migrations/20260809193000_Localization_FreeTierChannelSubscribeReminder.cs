using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <summary>
    /// Localized Free/Default channel-subscribe reminder (en / el / ru) with {channel} and {channelUrl}.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809193000_Localization_FreeTierChannelSubscribeReminder")]
    public partial class Localization_FreeTierChannelSubscribeReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                columns: new[] { "Id", "Key", "Language", "Text" },
                values: new object[,]
                {
                    {
                        103,
                        "FreeTierChannelSubscribeReminder",
                        1,
                        "📢 Please subscribe to our Telegram channel to keep using Free/Default VPN access.\n\nChannel: {channel}\n{channelUrl}"
                    },
                    {
                        104,
                        "FreeTierChannelSubscribeReminder",
                        2,
                        "📢 Παρακαλούμε εγγραφείτε στο κανάλι μας στο Telegram για να συνεχίσετε να χρησιμοποιείτε το Free/Default VPN.\n\nΚανάλι: {channel}\n{channelUrl}"
                    },
                    {
                        105,
                        "FreeTierChannelSubscribeReminder",
                        3,
                        "📢 Пожалуйста, подпишитесь на наш Telegram-канал, чтобы продолжать пользоваться VPN на тарифе Free/Default.\n\nКанал: {channel}\n{channelUrl}"
                    }
                },
                columnTypes: new[] { "integer", "character varying(255)", "integer", "text" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                keyColumn: "Id",
                keyValue: 103,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                keyColumn: "Id",
                keyValue: 104,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                keyColumn: "Id",
                keyValue: 105,
                keyColumnType: "integer");
        }
    }
}
