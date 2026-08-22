using DataGateMonitor.Models;

namespace DataGateMonitor.Services.Api.Interfaces;

public interface IVpnServerOvpnFileConfigService
{
    Task<VpnServerOvpnFileConfig> GetVpnServerOvpnFileConfigByServerId(int vpnServerId, 
        CancellationToken cancellationToken);

    /// <summary>Xray export endpoint hostname when config IP is empty: node PublicIp, then ApiUrl host.</summary>
    Task<string?> ResolveXrayExportEndpointHostAsync(int vpnServerId, CancellationToken cancellationToken);

    Task<VpnServerOvpnFileConfig> AddOrUpdateVpnServerOvpnFileConfigByServerId(
        VpnServerOvpnFileConfig openVpnServerOvpnFileConfig, bool autoDetectServerSettings, CancellationToken cancellationToken);
}