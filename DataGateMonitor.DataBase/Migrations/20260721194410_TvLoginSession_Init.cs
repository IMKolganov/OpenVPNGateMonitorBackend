using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class TvLoginSession_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TvLoginSessions",
                schema: "xgb_dashopnvpn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Client = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedUserId = table.Column<int>(type: "integer", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreateDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvLoginSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvLoginSessions_ExpiresAt",
                schema: "xgb_dashopnvpn",
                table: "TvLoginSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TvLoginSessions_Status",
                schema: "xgb_dashopnvpn",
                table: "TvLoginSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TvLoginSessions_UserCode",
                schema: "xgb_dashopnvpn",
                table: "TvLoginSessions",
                column: "UserCode");

            migrationBuilder.CreateIndex(
                name: "IX_TvLoginSessions_UserCode_Status",
                schema: "xgb_dashopnvpn",
                table: "TvLoginSessions",
                columns: new[] { "UserCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvLoginSessions",
                schema: "xgb_dashopnvpn");
        }
    }
}
