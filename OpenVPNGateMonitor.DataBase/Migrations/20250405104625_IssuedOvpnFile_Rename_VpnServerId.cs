using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenVPNGateMonitor.DataBase.Migrations
{
    /// <inheritdoc />
    public partial class IssuedOvpnFile_Rename_VpnServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ServerId",
                schema: "xgb_dashopnvpn",
                table: "IssuedOvpnFiles",
                newName: "VpnServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VpnServerId",
                schema: "xgb_dashopnvpn",
                table: "IssuedOvpnFiles",
                newName: "ServerId");
        }
    }
}
