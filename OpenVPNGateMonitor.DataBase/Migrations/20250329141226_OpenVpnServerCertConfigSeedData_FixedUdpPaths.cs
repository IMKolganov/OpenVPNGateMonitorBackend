using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerCertConfigSeedData_FixedUdpPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerCertConfigs",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CaCertPath", "CrlOpenvpnPath", "CrlPkiPath", "EasyRsaPath", "OvpnFileDir", "PkiPath", "RevokedOvpnFilesDirPath", "TlsAuthKey" },
                values: new object[] { "/openvpn-udp/easy-rsa/pki/ca.crt", "/openvpn-udp/crl.pem", "/openvpn-udp/easy-rsa/pki/crl.pem", "/openvpn-udp/easy-rsa", "/openvpn-udp/clients", "/openvpn-udp/easy-rsa/pki/", "/openvpn-udp/clients/revoked/", "/openvpn-udp/easy-rsa/pki/ta.key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "xgb_dashopnvpn",
                table: "OpenVpnServerCertConfigs",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CaCertPath", "CrlOpenvpnPath", "CrlPkiPath", "EasyRsaPath", "OvpnFileDir", "PkiPath", "RevokedOvpnFilesDirPath", "TlsAuthKey" },
                values: new object[] { "/openvpn-upd/easy-rsa/pki/ca.crt", "/openvpn-upd/crl.pem", "/openvpn-upd/easy-rsa/pki/crl.pem", "/openvpn-upd/easy-rsa", "/openvpn-upd/clients", "/openvpn-upd/easy-rsa/pki/", "/openvpn-upd/clients/revoked/", "/openvpn-upd/easy-rsa/pki/ta.key" });
        }
    }
}
