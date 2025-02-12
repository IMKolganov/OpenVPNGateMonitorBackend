using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.Api;

public interface IVpnDataService
{
    Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(int vpnServerId, CancellationToken cancellationToken);
    Task<List<OpenVpnServerClient>> GetAllHistoryOpenVpnServerClients(int vpnServerId, CancellationToken cancellationToken);
    Task<List<OpenVpnServerInfoResponse>> GetAllOpenVpnServers(CancellationToken cancellationToken);
    Task<OpenVpnServerInfoResponse> GetOpenVpnServerWithStats(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> GetOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> UpdateOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> DeleteOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
}