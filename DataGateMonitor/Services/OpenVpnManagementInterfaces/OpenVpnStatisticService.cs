using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace DataGateMonitor.Services.OpenVpnManagementInterfaces;

/// <summary>
/// Server-level byte totals from OpenVPN <c>status 3</c> CLIENT_LIST.
/// Prefer this over <c>load-stats</c>: with DCO, OpenVPN refreshes peer counters from the kernel
/// via <c>dco_get_peer_stats_multi</c> before printing status, while load-stats has no such pull
/// and undercounts data-plane traffic.
/// </summary>
public class OpenVpnSummaryStatService(
    ILogger<IOpenVpnSummaryStatService> logger,
    IOpenVpnClientService openVpnClientService)
    : IOpenVpnSummaryStatService
{
    public async Task<OpenVpnSummaryStats> GetSummaryStatsAsync(VpnServer openVpnServer,
        CancellationToken cancellationToken)
    {
        var status = await openVpnClientService.GetClientsFromManagementAsync(openVpnServer, cancellationToken);
        var stats = FromClientList(status);
        logger.LogInformation(
            "VpnServerId: {Id}. Summary stats from CLIENT_LIST " +
            "(dco={Dco}, nclients={Clients}, bytesin={BytesIn}, bytesout={BytesOut})",
            openVpnServer.Id, stats.DcoEnabled, stats.ClientsCount, stats.BytesIn, stats.BytesOut);
        return stats;
    }

    internal static OpenVpnSummaryStats FromClientList(OpenVpnManagementStatusResult status)
    {
        long bytesIn = 0;
        long bytesOut = 0;
        foreach (var c in status.Clients)
        {
            bytesIn += c.BytesReceived;
            bytesOut += c.BytesSent;
        }

        return new OpenVpnSummaryStats
        {
            ClientsCount = status.Clients.Count,
            BytesIn = bytesIn,
            BytesOut = bytesOut,
            DcoEnabled = status.DcoEnabled
        };
    }
}
