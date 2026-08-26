using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260531120000_TrafficDailyRollupNotificationKinds")]
    public partial class TrafficDailyRollupNotificationKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "VpnProfileNotificationPreferences",
                columns: new[] { "Id", "Enabled", "Kind" },
                values: new object[,]
                {
                    { 30, true, 29 },
                    { 31, true, 30 }
                });

            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('"xgb_dashopnvpn"."VpnProfileNotificationPreferences"', 'Id'),
                    (SELECT COALESCE(MAX("Id"), 1) FROM "xgb_dashopnvpn"."VpnProfileNotificationPreferences"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "VpnProfileNotificationPreferences",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "VpnProfileNotificationPreferences",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('"xgb_dashopnvpn"."VpnProfileNotificationPreferences"', 'Id'),
                    (SELECT COALESCE(MAX("Id"), 1) FROM "xgb_dashopnvpn"."VpnProfileNotificationPreferences"));
                """);
        }
    }
}
