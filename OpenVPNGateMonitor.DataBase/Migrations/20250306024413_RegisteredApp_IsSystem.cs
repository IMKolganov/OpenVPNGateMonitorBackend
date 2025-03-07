using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class RegisteredApp_IsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                schema: "xgb_dashopnvpn",
                table: "RegisteredApps",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystem",
                schema: "xgb_dashopnvpn",
                table: "RegisteredApps");
        }
    }
}
