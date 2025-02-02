using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOpenVpnServerService
{
    Task<List<OpenVpnClient>> GetConnectedClientsAsync(CancellationToken cancellationToken);
    Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken);
}