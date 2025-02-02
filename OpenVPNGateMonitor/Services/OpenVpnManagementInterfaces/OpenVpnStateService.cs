using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnStateService
{
    private readonly ILogger<OpenVpnStateService> _logger;
    private readonly IOpenVpnManagementService _openVpnManagementService;

    public OpenVpnStateService(ILogger<OpenVpnStateService> logger, IOpenVpnManagementService openVpnManagementService)
    {
        _logger = logger;
        _openVpnManagementService = openVpnManagementService;
    }
    
    public async Task<OpenVpnState> GetStateAsync(CancellationToken cancellationToken)
    {
        var response = await _openVpnManagementService.SendCommandAsync("state", cancellationToken);
        return ParseState(response);
    }
    
    private OpenVpnState ParseState(string data)
    {
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        OpenVpnState state = new();
        
        foreach (var line in lines)
        {
            var parts = line.Split(",");
            if (parts.Length < 5) continue;
            if (!string.IsNullOrWhiteSpace(parts[0]))
            {
                if (long.TryParse(parts[0], out long timestamp))
                {
                    state.UpSince = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
                else
                {
                    throw new Exception("Invalid date.");
                }
            }
            state.Connected = parts[1] == "CONNECTED";
            state.Success = parts[2] == "SUCCESS";
            state.LocalIp = parts[3];
            state.RemoteIp = parts[4];
        }
        return state;
    }

}