using DataGateMonitor.DataBase.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260721210000_TvLoginSession_OpenUserCodeUnique")]
public partial class TvLoginSession_OpenUserCodeUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TvLoginSessions_UserCode_Open",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions",
            column: "UserCode",
            unique: true,
            filter: "\"Status\" IN (0, 5)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TvLoginSessions_UserCode_Open",
            schema: "xgb_dashopnvpn",
            table: "TvLoginSessions");
    }
}
