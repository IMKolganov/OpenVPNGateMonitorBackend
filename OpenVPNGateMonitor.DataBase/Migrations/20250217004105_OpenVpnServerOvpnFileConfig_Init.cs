using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class OpenVpnServerOvpnFileConfig_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CertName",
                schema: "xgb_dashopnvpn",
                table: "IssuedOvpnFiles",
                newName: "CommonName");

            migrationBuilder.CreateTable(
                name: "OpenVpnServerOvpnFileConfigs",
                schema: "xgb_dashopnvpn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    VpnServerIp = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    VpnServerPort = table.Column<int>(type: "integer", nullable: false),
                    ConfigTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenVpnServerOvpnFileConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenVpnServerOvpnFileConfigs",
                schema: "xgb_dashopnvpn");

            migrationBuilder.RenameColumn(
                name: "CommonName",
                schema: "xgb_dashopnvpn",
                table: "IssuedOvpnFiles",
                newName: "CertName");
        }
    }
}
