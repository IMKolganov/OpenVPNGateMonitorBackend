using OpenVPNGateMonitor.Models.Enums;

namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnBackgroundService
{
    DateTime GetNextRunTime();
    BackgroundServiceStatus GetStatus();
    Task RunNow(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    void Dispose();
    Task? ExecuteTask { get; }
}