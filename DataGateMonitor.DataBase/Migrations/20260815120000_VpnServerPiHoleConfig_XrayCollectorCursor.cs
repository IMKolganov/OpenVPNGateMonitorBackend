using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

/// <summary>
/// Prod history alignment only: this migration id is already recorded on production
/// (column <c>LastCursorUntilUtc</c> on <c>VpnServerPiHoleConfigs</c>). No-op in code — do not remove.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260815120000_VpnServerPiHoleConfig_XrayCollectorCursor")]
public partial class VpnServerPiHoleConfig_XrayCollectorCursor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
