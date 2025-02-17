using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class IssuedOvpnFile_init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssuedOvpnFiles",
                schema: "xgb_dashopnvpn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CertName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CertId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedTo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PemFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CertFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    KeyFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReqFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedOvpnFiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssuedOvpnFiles",
                schema: "xgb_dashopnvpn");
        }
    }
}
