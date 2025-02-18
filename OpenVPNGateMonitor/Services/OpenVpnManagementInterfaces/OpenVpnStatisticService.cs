using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStatService : IOpenVpnSummaryStatService
{
    private readonly ILogger<IOpenVpnSummaryStatService> _logger;
    private readonly OpenVpnManagerPool _openVpnManagerPool;
    
    public OpenVpnSummaryStatService(ILogger<IOpenVpnSummaryStatService> logger, OpenVpnManagerPool openVpnManagerPool)
    {
        _logger = logger;
        _openVpnManagerPool = openVpnManagerPool;
    }
    
    public async Task<OpenVpnSummaryStats> GetSummaryStatsAsync(string managementIp, int managementPort, 
        CancellationToken cancellationToken)
    {
        var manager = _openVpnManagerPool.GetOrCreateManager(managementIp, managementPort);
        var response = await manager.SendCommandAsync("load-stats", cancellationToken);
        return ParseSummaryStats(response);
    }
    
    private OpenVpnSummaryStats ParseSummaryStats(string data)
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