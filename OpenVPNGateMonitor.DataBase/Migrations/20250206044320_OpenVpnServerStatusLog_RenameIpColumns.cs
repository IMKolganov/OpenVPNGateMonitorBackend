using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerStatusLog_RenameIpColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RemoteIp",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs",
                newName: "ServerRemoteIp");

            migrationBuilder.RenameColumn(
                name: "LocalIp",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs",
                newName: "ServerLocalIp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ServerRemoteIp",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs",
                newName: "RemoteIp");

            migrationBuilder.RenameColumn(
                name: "ServerLocalIp",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerStatusLogs",
                newName: "LocalIp");
        }
    }
}
