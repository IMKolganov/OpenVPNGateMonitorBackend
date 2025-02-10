namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnServerService
{
    Task SaveConnectedClientsAsync(int vpnServerId, string managementIp, 
        int managementPort, CancellationToken cancellationToken);
    Task SaveOpenVpnServerStatusLogAsync(int vpnServerId, string managementIp, 
        int managementPort, CancellationToken cancellationToken);
}