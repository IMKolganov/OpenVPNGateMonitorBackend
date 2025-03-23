using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class ServiceStatusResponse
{
    public int VpnServerId { get; set; }
    public ServiceStatus Status { get; set; } = ServiceStatus.Idle;
    public string? ErrorMessage { get; set; }
    public DateTime NextRunTime { get; set; }
}
