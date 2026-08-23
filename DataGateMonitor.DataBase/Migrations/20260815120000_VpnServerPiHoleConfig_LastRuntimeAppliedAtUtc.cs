using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260815120000_VpnServerPiHoleConfig_LastRuntimeAppliedAtUtc")]
public partial class VpnServerPiHoleConfig_LastRuntimeAppliedAtUtc : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: column may already exist if a previous deploy applied DDL
        // without recording this migration (or schema was patched manually).
        migrationBuilder.Sql(
            """
            ALTER TABLE xgb_dashopnvpn."VpnServerPiHoleConfigs"
            ADD COLUMN IF NOT EXISTS "LastRuntimeAppliedAtUtc" timestamp with time zone NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE xgb_dashopnvpn."VpnServerPiHoleConfigs"
            DROP COLUMN IF EXISTS "LastRuntimeAppliedAtUtc";
            """);
    }
}
