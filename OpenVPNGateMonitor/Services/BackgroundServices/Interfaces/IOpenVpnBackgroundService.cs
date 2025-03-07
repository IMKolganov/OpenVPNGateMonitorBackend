using OpenVPNGateMonitor.Models.Helpers.Background;

namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnBackgroundService
{
    public Dictionary<int, BackgroundServerStatus> GetStatus();
    Task RunNow(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    void Dispose();
    Task? ExecuteTask { get; }
}