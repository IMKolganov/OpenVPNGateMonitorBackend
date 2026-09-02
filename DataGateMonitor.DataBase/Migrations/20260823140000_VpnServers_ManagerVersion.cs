using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260823140000_VpnServers_ManagerVersion")]
public partial class VpnServers_ManagerVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE xgb_dashopnvpn."VpnServers"
                ADD COLUMN IF NOT EXISTS "ManagerVersion" character varying(64) NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE xgb_dashopnvpn."VpnServers"
                DROP COLUMN IF EXISTS "ManagerVersion";
            """);
    }
}
