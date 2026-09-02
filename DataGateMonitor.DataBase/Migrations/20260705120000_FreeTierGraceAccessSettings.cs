using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260705120000_FreeTierGraceAccessSettings")]
    public partial class FreeTierGraceAccessSettings : Migration
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
                    { 19, false, null, null, null, "FreeTier_Allow_Grace_Without_Compliance", null, "bool" },
                    { 20, null, null, null, null, "FreeTier_Allow_Grace_Without_Compliance_Type", "bool", "string" },
                    { 21, null, null, null, 5, "FreeTier_Grace_Period_Minutes", null, "int" },
                    { 22, null, null, null, null, "FreeTier_Grace_Period_Minutes_Type", "int", "string" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "Settings",
                keyColumn: "Id",
                keyValue: 22);
        }
    }
}
