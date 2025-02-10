using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api;

public interface IVpnDataService
{
    Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(CancellationToken cancellationToken);
    Task<List<OpenVpnServerClient>> GetAllHistoryOpenVpnServerClients(CancellationToken cancellationToken);
    // Task<List<OpenVpnServerStatusLog>> GetOpenVpnServers(CancellationToken cancellationToken);
    Task<OpenVpnServerStatusLog?> GetOpenVpnServerStatusLog(CancellationToken cancellationToken);
    Task<List<OpenVpnServer>> GetAllOpenVpnServers(CancellationToken cancellationToken);
    Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> DeleteOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
}