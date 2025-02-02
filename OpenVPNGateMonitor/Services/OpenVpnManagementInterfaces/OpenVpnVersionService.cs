using System.Text.RegularExpressions;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnVersionService : IOpenVpnVersionService
{
    private readonly ILogger<OpenVpnVersionService> _logger;
    private readonly IOpenVpnManagementService _openVpnManagementService;
    
    public OpenVpnVersionService(ILogger<OpenVpnVersionService> logger, 
        IOpenVpnManagementService openVpnManagementService)
    {
        _logger = logger;
        _openVpnManagementService = openVpnManagementService;
    }
    
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        string response = await _openVpnManagementService.SendCommandAsync("version", cancellationToken);
        
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