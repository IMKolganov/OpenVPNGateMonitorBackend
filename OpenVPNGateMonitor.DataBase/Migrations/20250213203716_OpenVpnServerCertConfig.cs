using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerCertConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenVpnServerCertConfigs",
                schema: "xgb_dashopnvpn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VpnServerId = table.Column<int>(type: "integer", nullable: false),
                    EasyRsaPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OvpnFileDir = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RevokedOvpnFilesDirPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PkiPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CaCertPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TlsAuthKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ServerRemoteIp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CrlPkiPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CrlOpenvpnPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StatusFilePath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenVpnServerCertConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenVpnServerCertConfigs",
                schema: "xgb_dashopnvpn");
        }
    }
}
