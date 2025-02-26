using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOpenVpnServerOvpnFileConfigService
{
    Task<OpenVpnServerOvpnFileConfig> GetOpenVpnServerOvpnFileConfigByServerId(int vpnServerId, 
        CancellationToken cancellationToken);

    Task<OpenVpnServerOvpnFileConfig> AddOrUpdateOpenVpnServerOvpnFileConfigByServerId(
        OpenVpnServerOvpnFileConfig openVpnServerOvpnFileConfig, CancellationToken cancellationToken);
}