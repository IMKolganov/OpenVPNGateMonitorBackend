using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.OpenVpnProxy;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace DataGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStatService(
    ILogger<IOpenVpnSummaryStatService> logger,
    IOpenVpnMicroserviceClientFactory openVpnMicroserviceClientFactory,
    IOpenVpnClientService openVpnClientService)
    : IOpenVpnSummaryStatService
{
    public async Task<OpenVpnSummaryStats> GetSummaryStatsAsync(VpnServer openVpnServer,
        CancellationToken cancellationToken)
    {
        // status 3 carries GLOBAL_STATS dco_enabled and per-client byte counters.
        // With DCO, load-stats stays near handshake size; CLIENT_LIST tracks data-plane bytes.
        var status = await openVpnClientService.GetClientsFromManagementAsync(openVpnServer, cancellationToken);
        if (status.DcoEnabled == true)
        {
            var fromClients = FromClientList(status);
            logger.LogInformation(
                "VpnServerId: {Id}. DCO enabled — using CLIENT_LIST sum for summary stats " +
                "(nclients={Clients}, bytesin={BytesIn}, bytesout={BytesOut})",
                openVpnServer.Id, fromClients.ClientsCount, fromClients.BytesIn, fromClients.BytesOut);
            return fromClients;
        }

        var client = openVpnMicroserviceClientFactory.Create(openVpnServer);
        var response = await client.SendCommandWithResponseAsync("load-stats", cancellationToken);
        logger.LogDebug("Received summary stats response:\n{Response}", response);
        var stats = ParseSummaryStats(response);
        stats.DcoEnabled = status.DcoEnabled;
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
            DcoEnabled = true,
            UsedClientListFallback = true
        };
    }

    private static OpenVpnSummaryStats ParseSummaryStats(string data)
    {
        OpenVpnSummaryStats stats = new();
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("nclients="))
            {
                stats.ClientsCount = int.Parse(line.Split("nclients=")[1].Split(',')[0]);
            }
            if (line.Contains("bytesin="))
            {
                stats.BytesIn = long.Parse(line.Split("bytesin=")[1].Split(',')[0]);
            }
            if (line.Contains("bytesout="))
            {
                stats.BytesOut = long.Parse(line.Split("bytesout=")[1].Split(',')[0]);
            }
        }
        return stats;
    }
}
