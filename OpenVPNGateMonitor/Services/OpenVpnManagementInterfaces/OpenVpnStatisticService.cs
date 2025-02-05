using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnSummaryStatService : IOpenVpnSummaryStatService
{
    private readonly ILogger<IOpenVpnSummaryStatService> _logger;
    private readonly IOpenVpnManagementService _openVpnManagementService;
    
    public OpenVpnSummaryStatService(ILogger<IOpenVpnSummaryStatService> logger, IOpenVpnManagementService openVpnManagementService)
    {
        _logger = logger;
        _openVpnManagementService = openVpnManagementService;
    }
    
    public async Task<OpenVpnSummaryStats> GetSummaryStatsAsync(CancellationToken cancellationToken)
    {
        var response = await _openVpnManagementService.SendCommandAsync("load-stats", cancellationToken);
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