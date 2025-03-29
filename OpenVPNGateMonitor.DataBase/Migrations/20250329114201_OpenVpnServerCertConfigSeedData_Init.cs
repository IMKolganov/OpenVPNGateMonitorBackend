using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerCertConfigSeedData_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerCertConfigs",
                columns: new[] { "Id", "CaCertPath", "CrlOpenvpnPath", "CrlPkiPath", "EasyRsaPath", "OvpnFileDir", "PkiPath", "RevokedOvpnFilesDirPath", "ServerRemoteIp", "StatusFilePath", "TlsAuthKey", "VpnServerId" },
                values: new object[,]
                {
                    { 1, "/openvpn-upd/easy-rsa/pki/ca.crt", "/openvpn-upd/crl.pem", "/openvpn-upd/easy-rsa/pki/crl.pem", "/openvpn-upd/easy-rsa", "/openvpn-upd/clients", "/openvpn-upd/easy-rsa/pki/", "/openvpn-upd/clients/revoked/", "0.0.0.0", "/var/log/openvpn-status.log", "/openvpn-upd/easy-rsa/pki/ta.key", 1 },
                    { 2, "/openvpn-tcp/easy-rsa/pki/ca.crt", "/openvpn-tcp/crl.pem", "/openvpn-tcp/easy-rsa/pki/crl.pem", "/openvpn-tcp/easy-rsa", "/openvpn-tcp/clients", "/openvpn-tcp/easy-rsa/pki/", "/openvpn-tcp/clients/revoked/", "0.0.0.0", "/var/log/openvpn-status.log", "/openvpn-tcp/easy-rsa/pki/ta.key", 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerCertConfigs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerCertConfigs",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
