using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class VpnServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerClients",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers");

            migrationBuilder.DropColumn(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerClients");
        }
    }
}
