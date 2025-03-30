using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerOvpnFileConfigSeedData_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                newName: "VpnServerId");

            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                columns: new[] { "Id", "ConfigTemplate", "VpnServerId", "VpnServerIp", "VpnServerPort" },
                values: new object[,]
                {
                    { 1, "client\r\ndev tun\r\nproto udp\r\nremote {{server_ip}} {{server_port}}\r\nresolv-retry infinite\r\nnobind\r\nremote-cert-tls server\r\ntls-version-min 1.2\r\ncipher AES-256-CBC\r\nauth SHA256\r\nauth-nocache\r\nverb 3\r\n<ca>\r\n{{ca_cert}}\r\n</ca>\r\n<cert>\r\n{{client_cert}}\r\n</cert>\r\n<key>\r\n{{client_key}}\r\n</key>\r\n<tls-crypt>\r\n{{tls_auth_key}}\r\n</tls-crypt>", 1, "127.0.0.1", 1194 },
                    { 2, "client\r\ndev tun\r\nproto tcp\r\nremote {{server_ip}} {{server_port}}\r\nresolv-retry infinite\r\nnobind\r\nremote-cert-tls server\r\ntls-version-min 1.2\r\ncipher AES-256-CBC\r\nauth SHA256\r\nauth-nocache\r\nverb 3\r\n<ca>\r\n{{ca_cert}}\r\n</ca>\r\n<cert>\r\n{{client_cert}}\r\n</cert>\r\n<key>\r\n{{client_key}}\r\n</key>\r\n<tls-crypt>\r\n{{tls_auth_key}}\r\n</tls-crypt>", 2, "127.0.0.1", 1195 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.RenameColumn(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                newName: "ServerId");
        }
    }
}
