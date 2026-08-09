using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260721203000_TvLoginSession_ApprovedUserIndexes")]
public partial class TvLoginSession_ApprovedUserIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TvLoginSessions_ApprovedUserId",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions",
            column: "ApprovedUserId");

        migrationBuilder.CreateIndex(
            name: "IX_TvLoginSessions_ApprovedUserId_Status",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions",
            columns: new[] { "ApprovedUserId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TvLoginSessions_ApprovedUserId_Status",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions");

        migrationBuilder.DropIndex(
            name: "IX_TvLoginSessions_ApprovedUserId",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions");
    }
}
