namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnBackgroundService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    void Dispose();
    Task? ExecuteTask { get; }
}