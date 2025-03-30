using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerSeedData_UpdServerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ManagementIp", "ManagementPort" },
                values: new object[] { "openvpn_udp", 5092 });

            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ManagementIp", "ManagementPort" },
                values: new object[] { "openvpn_tcp", 5093 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ManagementIp", "ManagementPort" },
                values: new object[] { "127.0.0.1", 5093 });

            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServers",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ManagementIp", "ManagementPort" },
                values: new object[] { "127.0.0.1", 5092 });
        }
    }
}
