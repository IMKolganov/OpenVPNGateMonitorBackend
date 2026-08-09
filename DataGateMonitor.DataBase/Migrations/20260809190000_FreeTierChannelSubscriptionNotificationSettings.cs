using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Designer-less data migration: columnTypes required so EF can generate SQL
    /// without a BuildTargetModel that maps Settings.
    /// </remarks>
    public partial class FreeTierChannelSubscriptionNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                columns: new[] { "Id", "BoolValue", "DateTimeValue", "DoubleValue", "IntValue", "Key", "StringValue", "ValueType" },
                values: new object[,]
                {
                    { 202, true, null, null, null, "FreeTier_Send_Unsubscribed_User_Reminders", null, "bool" },
                    { 203, null, null, null, null, "FreeTier_Send_Unsubscribed_User_Reminders_Type", "bool", "string" },
                    { 204, true, null, null, null, "FreeTier_Daily_Unsubscribed_Admin_Digest", null, "bool" },
                    { 205, null, null, null, null, "FreeTier_Daily_Unsubscribed_Admin_Digest_Type", "bool", "string" }
                },
                columnTypes: new[]
                {
                    "integer",
                    "boolean",
                    "timestamp with time zone",
                    "double precision",
                    "integer",
                    "character varying(255)",
                    "character varying(255)",
                    "character varying(50)"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 202,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 203,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 204,
                keyColumnType: "integer");

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 205,
                keyColumnType: "integer");
        }
    }
}
