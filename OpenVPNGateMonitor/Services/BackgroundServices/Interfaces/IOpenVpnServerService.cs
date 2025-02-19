using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IOpenVpnServerService
{
    Task SaveConnectedClientsAsync(int vpnServerId, ICommandQueue commandQueue, CancellationToken cancellationToken);
    Task SaveOpenVpnServerStatusLogAsync(int vpnServerId, ICommandQueue commandQueue,
        CancellationToken cancellationToken);
}