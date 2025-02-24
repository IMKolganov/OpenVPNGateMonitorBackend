using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.Models.Helpers.Background;

public class BackgroundServerStatus
{
    public ServiceStatus Status { get; set; } = ServiceStatus.Idle;
    public string? ErrorMessage { get; set; }
    public DateTime NextRunTime { get; set; }
}