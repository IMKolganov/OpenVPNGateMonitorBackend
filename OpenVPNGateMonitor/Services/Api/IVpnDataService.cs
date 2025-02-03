using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api;

public interface IVpnDataService
{
    Task<List<OpenVpnServerClient>> GetAllConnectedOpenVpnServerClients(CancellationToken cancellationToken);
    Task<List<OpenVpnServerClient>> GetAllHistoryOpenVpnServerClients(CancellationToken cancellationToken);

    Task<OpenVpnServerStatusLog?> GetOpenVpnServerStatusLog(CancellationToken cancellationToken);
}