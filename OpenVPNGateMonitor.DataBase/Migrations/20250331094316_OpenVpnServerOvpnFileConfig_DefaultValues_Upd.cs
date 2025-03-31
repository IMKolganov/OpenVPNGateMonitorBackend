using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerOvpnFileConfig_DefaultValues_Upd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 1,
                column: "ConfigTemplate",
                value: "client\ndev tun\nproto udp\nremote {{server_ip}} {{server_port}}\nresolv-retry infinite\nnobind\nremote-cert-tls server\ntls-version-min 1.2\ncipher AES-256-CBC\nauth SHA256\nauth-nocache\nverb 3\n<ca>\n{{ca_cert}}\n</ca>\n<cert>\n{{client_cert}}\n</cert>\n<key>\n{{client_key}}\n</key>\n<tls-crypt>\n{{tls_auth_key}}\n</tls-crypt>");

            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 2,
                column: "ConfigTemplate",
                value: "client\ndev tun\nproto tcp\nremote {{server_ip}} {{server_port}}\nresolv-retry infinite\nnobind\nremote-cert-tls server\ntls-version-min 1.2\ncipher AES-256-CBC\nauth SHA256\nauth-nocache\nverb 3\n<ca>\n{{ca_cert}}\n</ca>\n<cert>\n{{client_cert}}\n</cert>\n<key>\n{{client_key}}\n</key>\n<tls-crypt>\n{{tls_auth_key}}\n</tls-crypt>");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 1,
                column: "ConfigTemplate",
                value: "client\r\ndev tun\r\nproto udp\r\nremote {{server_ip}} {{server_port}}\r\nresolv-retry infinite\r\nnobind\r\nremote-cert-tls server\r\ntls-version-min 1.2\r\ncipher AES-256-CBC\r\nauth SHA256\r\nauth-nocache\r\nverb 3\r\n<ca>\r\n{{ca_cert}}\r\n</ca>\r\n<cert>\r\n{{client_cert}}\r\n</cert>\r\n<key>\r\n{{client_key}}\r\n</key>\r\n<tls-crypt>\r\n{{tls_auth_key}}\r\n</tls-crypt>");

            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerOvpnFileConfigs",
                keyColumn: "Id",
                keyValue: 2,
                column: "ConfigTemplate",
                value: "client\r\ndev tun\r\nproto tcp\r\nremote {{server_ip}} {{server_port}}\r\nresolv-retry infinite\r\nnobind\r\nremote-cert-tls server\r\ntls-version-min 1.2\r\ncipher AES-256-CBC\r\nauth SHA256\r\nauth-nocache\r\nverb 3\r\n<ca>\r\n{{ca_cert}}\r\n</ca>\r\n<cert>\r\n{{client_cert}}\r\n</cert>\r\n<key>\r\n{{client_key}}\r\n</key>\r\n<tls-crypt>\r\n{{tls_auth_key}}\r\n</tls-crypt>");
        }
    }
}
