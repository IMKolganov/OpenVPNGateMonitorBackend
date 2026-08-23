using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class AuthAdminIdleTimeoutSetting : Migration
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
                    { 206, null, null, null, 15, "Auth_Admin_Idle_Timeout_Minutes", null, "int" },
                    { 207, null, null, null, null, "Auth_Admin_Idle_Timeout_Minutes_Type", "int", "string" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 206);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 207);
        }
    }
}
