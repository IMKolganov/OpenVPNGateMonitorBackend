using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260814220000_VpnServers_ServerNameUniqueActiveOnly")]
public partial class VpnServers_ServerNameUniqueActiveOnly : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VpnServers_ServerName",
            schema: "xgb_dashopnvpn",
            table: "VpnServers");

        migrationBuilder.CreateIndex(
            name: "IX_VpnServers_ServerName",
            schema: "xgb_dashopnvpn",
            table: "VpnServers",
            column: "ServerName",
            unique: true,
            filter: "\"IsDeleted\" = FALSE");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VpnServers_ServerName",
            schema: "xgb_dashopnvpn",
            table: "VpnServers");

        migrationBuilder.CreateIndex(
            name: "IX_VpnServers_ServerName",
            schema: "xgb_dashopnvpn",
            table: "VpnServers",
            column: "ServerName",
            unique: true);
    }
}
