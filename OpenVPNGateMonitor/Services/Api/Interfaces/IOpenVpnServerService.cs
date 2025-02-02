namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOpenVpnServerService
{
    Task SaveConnectedClientsAsync(CancellationToken cancellationToken);
    Task SaveOpenVpnServerStatusLogAsync(CancellationToken cancellationToken);
}