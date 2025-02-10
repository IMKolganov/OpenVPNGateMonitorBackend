namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnServerService
{
    Task SaveConnectedClientsAsync(string managementIp, int managementPort, CancellationToken cancellationToken);
    Task SaveOpenVpnServerStatusLogAsync(string managementIp, int managementPort, CancellationToken cancellationToken);
}