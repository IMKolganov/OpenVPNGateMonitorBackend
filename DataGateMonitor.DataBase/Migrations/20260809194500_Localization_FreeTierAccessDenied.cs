using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <summary>
    /// Localized Free/Default VPN access-denied message (en / el / ru) with {channel} and {channelUrl}.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809194500_Localization_FreeTierAccessDenied")]
    public partial class Localization_FreeTierAccessDenied : Migration
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
                        106,
                        "FreeTierAccessDenied",
                        1,
                        "Free/Default access requires subscription to {channel}.\n{channelUrl}\nPlease subscribe to the channel and try again."
                    },
                    {
                        107,
                        "FreeTierAccessDenied",
                        2,
                        "Η πρόσβαση Free/Default απαιτεί συνδρομή στο κανάλι {channel}.\n{channelUrl}\nΠαρακαλούμε εγγραφείτε στο κανάλι και δοκιμάστε ξανά."
                    },
                    {
                        108,
                        "FreeTierAccessDenied",
                        3,
                        "Для тарифа Free/Default нужна подписка на канал {channel}.\n{channelUrl}\nПодпишитесь на канал и повторите запрос."
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
                keyValue: 106,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                keyColumn: "Id",
                keyValue: 107,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "LocalizationTexts",
                keyColumn: "Id",
                keyValue: 108,
                keyColumnType: "integer");
        }
    }
}
