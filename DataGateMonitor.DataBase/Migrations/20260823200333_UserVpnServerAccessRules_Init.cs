using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class UserVpnServerAccessRules_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserVpnServerAccessRules",
                schema: "xgb_dashopnvpn",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    VpnServerId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    CreateDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVpnServerAccessRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserVpnServerAccessRules_UserId_VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "UserVpnServerAccessRules",
                columns: new[] { "UserId", "VpnServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserVpnServerAccessRules_VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "UserVpnServerAccessRules",
                column: "VpnServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserVpnServerAccessRules",
                schema: "xgb_dashopnvpn");
        }
    }
}
