using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.Models.Helpers.Background;

public class BackgroundServerStatus
{
    public int VpnServerId { get; set; }
    public ServiceStatus Status { get; set; } = ServiceStatus.Idle;
    public string? ErrorMessage { get; set; }
    public DateTime NextRunTime { get; set; }
}