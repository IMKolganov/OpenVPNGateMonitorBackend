using System.Collections.Concurrent;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Background;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class OpenVpnServerStatusManager
{
    private readonly ConcurrentDictionary<int, BackgroundServerStatus> _serverStatuses = new();

    public void UpdateStatus(int serverId, ServiceStatus status, int nextRunSeconds, string? errorMessage = null)
    {
        _serverStatuses.AddOrUpdate(serverId,
            new BackgroundServerStatus
            {
                VpnServerId = serverId,
                Status = status, 
                ErrorMessage = errorMessage, 
                NextRunTime = DateTime.UtcNow.AddSeconds(nextRunSeconds)
            },
            (_, existing) =>
            {
                existing.VpnServerId = serverId;
                existing.Status = status;
                existing.ErrorMessage = errorMessage;
                existing.NextRunTime = DateTime.UtcNow.AddSeconds(nextRunSeconds);
                return existing;
            });
    }

    public BackgroundServerStatus GetStatus(int serverId)
    {
        return _serverStatuses.GetValueOrDefault(serverId, new BackgroundServerStatus());
    }

    public Dictionary<int, BackgroundServerStatus> GetAllStatuses()
    {
        return new Dictionary<int, BackgroundServerStatus>(_serverStatuses);
    }
}