using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IVpnDataService
{
    Task<OpenVpnServerClientsResponse> GetAllConnectedOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<OpenVpnServerClientsResponse> GetAllHistoryOpenVpnServerClients(
        int vpnServerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<List<OpenVpnServerInfoResponse>> GetAllOpenVpnServers(CancellationToken cancellationToken);
    Task<OpenVpnServerInfoResponse> GetOpenVpnServerWithStats(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> GetOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
    Task<OpenVpnServer> AddOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> UpdateOpenVpnServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken);
    Task<OpenVpnServer> DeleteOpenVpnServer(int vpnServerId, CancellationToken cancellationToken);
}