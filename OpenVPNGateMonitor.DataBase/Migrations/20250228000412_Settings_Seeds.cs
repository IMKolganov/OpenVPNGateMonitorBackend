using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class Settings_Seeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                columns: new[] { "Id", "BoolValue", "CreateDate", "DateTimeValue", "DoubleValue", "IntValue", "Key", "LastUpdate", "StringValue", "ValueType" },
                values: new object[,]
                {
                    { -4, null, new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8353), null, null, null, "OpenVPN_Polling_Interval_Unit_Type", new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8353), "string", "string" },
                    { -3, null, new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8351), null, null, null, "OpenVPN_Polling_Interval_Unit", new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8351), "seconds", "string" },
                    { -2, null, new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8349), null, null, null, "OpenVPN_Polling_Interval_Type", new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8348), "int", "string" },
                    { -1, null, new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8156), null, null, 120, "OpenVPN_Polling_Interval", new DateTime(2025, 2, 28, 0, 4, 12, 335, DateTimeKind.Utc).AddTicks(8053), null, "int" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: -4);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: -3);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: -2);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: -1);
        }
    }
}
