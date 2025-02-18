using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnStateService : IOpenVpnStateService
{
    private readonly ILogger<IOpenVpnStateService> _logger;
    private readonly OpenVpnManagerPool _openVpnManagerPool;

    public OpenVpnStateService(ILogger<IOpenVpnStateService> logger, OpenVpnManagerPool openVpnManagerPool)
    {
        _logger = logger;
        _openVpnManagerPool = openVpnManagerPool;
    }
    
    public async Task<OpenVpnState> GetStateAsync(string managementIp, int managementPort, 
        CancellationToken cancellationToken)
    {
        var manager = _openVpnManagerPool.GetOrCreateManager(managementIp, managementPort);
        var response = await manager.SendCommandAsync("state", cancellationToken);
        return ParseState(response);
    }
    
    private OpenVpnState ParseState(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            _logger.LogWarning("[STATE PARSER] Received empty response from OpenVPN.");
            return new OpenVpnState { Success = false };
        }

        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        OpenVpnState state = new();

        try
        {
            foreach (var line in lines)
            {
                var parts = line.Split(",");
                if (parts.Length < 5)
                {
                    _logger.LogWarning($"[STATE PARSER] Skipping malformed line: {line}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    if (long.TryParse(parts[0], out long timestamp))
                    {
                        state.UpSince = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                    }
                    else
                    {
                        _logger.LogError($"[STATE PARSER] Invalid date format: {parts[0]}");
                        throw new Exception($"Invalid date: {parts[0]}");
                    }
                }

                state.Connected = parts[1] == "CONNECTED";
                state.Success = parts[2] == "SUCCESS";
                state.ServerLocalIp = parts[3];
                state.ServerRemoteIp = parts[4];
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[STATE PARSER] Error while parsing state data: {data}");
            throw;
        }
    }
}