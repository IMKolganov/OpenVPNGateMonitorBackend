using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260815120500_VpnServerPiHoleConfig_LastRuntimeAppliedAtUtc")]
public partial class VpnServerPiHoleConfig_LastRuntimeAppliedAtUtc : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastRuntimeAppliedAtUtc",
            schema: "xgb_dashopnvpn",
            table: "VpnServerPiHoleConfigs",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastRuntimeAppliedAtUtc",
            schema: "xgb_dashopnvpn",
            table: "VpnServerPiHoleConfigs");
    }
}
