namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnServerService
{
    Task SaveConnectedClientsAsync(CancellationToken cancellationToken);
    Task SaveOpenVpnServerStatusLogAsync(CancellationToken cancellationToken);
}