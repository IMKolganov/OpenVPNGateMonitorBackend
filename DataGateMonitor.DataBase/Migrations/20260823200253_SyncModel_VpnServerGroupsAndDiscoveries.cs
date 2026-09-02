using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataGateMonitor.DataBase.Migrations
{
    /// <summary>
    /// No-op: aligns the EF model snapshot with <c>VpnServerGroups</c> / <c>VpnServerDiscoveries</c>
    /// (and related <c>VpnServers</c> columns) that were already applied by earlier SQL migrations.
    /// </summary>
    public partial class SyncModel_VpnServerGroupsAndDiscoveries : Migration
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
}
