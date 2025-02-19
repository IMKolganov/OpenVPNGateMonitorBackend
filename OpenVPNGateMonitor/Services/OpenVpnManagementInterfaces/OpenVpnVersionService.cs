using System.Text.RegularExpressions;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnVersionService : IOpenVpnVersionService
{
    private readonly ILogger<IOpenVpnVersionService> _logger;
    private readonly ICommandQueueManager _commandQueueManager;
    
    public OpenVpnVersionService(ILogger<IOpenVpnVersionService> logger, 
        ICommandQueueManager commandQueueManager)
    {
        _logger = logger;
        _commandQueueManager = commandQueueManager;
    }
    
    public async Task<string> GetVersionAsync(string managementIp, int managementPort, CancellationToken cancellationToken)
    {
        var manager = await _commandQueueManager.GetOrCreateQueueAsync(managementIp, managementPort);
        var response = await manager.SendCommandAsync("version");
        
        var (openVpnVersion, managementVersion) = ParseVersion(response);
        return openVpnVersion;
    }

    private (string OpenVpnVersion, int ManagementVersion) ParseVersion(string data)
    {
        string openVpnVersion = "Unknown";
        int managementVersion = -1;

        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("OpenVPN Version:", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"OpenVPN (\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    openVpnVersion = match.Groups[1].Value;
                }
            }
            else if (line.StartsWith("Management Version:", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"Management Version: (\d+)");
                if (match.Success)
                {
                    managementVersion = int.Parse(match.Groups[1].Value);
                }
            }
        }

        return (openVpnVersion, managementVersion);
    }

}