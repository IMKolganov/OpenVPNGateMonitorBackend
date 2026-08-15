using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;

namespace DataGateMonitor.Services.Api;

public class VpnServerOvpnFileConfigService(
    ILogger<VpnServerOvpnFileConfigService> logger,
    IVpnServerOvpnFileConfigQueryService openVpnServerOvpnFileConfigQueryService,
    ICommandService<VpnServerOvpnFileConfig, int> openVpnServerOvpnFileConfigCommandService,
    IMicroserviceInfoService microserviceInfoService,
    TimeSpan? openVpnAutoDetectTimeout = null)
    : IVpnServerOvpnFileConfigService
{
    private readonly ILogger<VpnServerOvpnFileConfigService> _logger = logger;
    private readonly TimeSpan _openVpnAutoDetectTimeout =
        openVpnAutoDetectTimeout ?? OpenVpnDetectedSettingsHelper.DefaultAutoDetectTimeout;

    public async Task<VpnServerOvpnFileConfig> GetVpnServerOvpnFileConfigByServerId(int vpnServerId,
        CancellationToken ct)
    {
        return await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(vpnServerId, ct)
               ?? throw new InvalidOperationException("OvpnFileConfig not found");
    }

    public async Task<VpnServerOvpnFileConfig> AddOrUpdateVpnServerOvpnFileConfigByServerId(
        VpnServerOvpnFileConfig openVpnServerOvpnFileConfig, bool autoDetectServerSettings, CancellationToken ct)
    {
        if (autoDetectServerSettings)
            await OpenVpnDetectedSettingsHelper.TryApplyAsync(
                openVpnServerOvpnFileConfig.VpnServerId,
                openVpnServerOvpnFileConfig,
                microserviceInfoService,
                _logger,
                "Failed to auto-detect OpenVPN client settings for VpnServerId={VpnServerId}. Keeping provided values.",
                ct,
                _openVpnAutoDetectTimeout);

        var existingConfig = await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
            openVpnServerOvpnFileConfig.VpnServerId, ct);

        if (existingConfig != null)
        {
            existingConfig.VpnServerIp = openVpnServerOvpnFileConfig.VpnServerIp;
            existingConfig.VpnServerPort = openVpnServerOvpnFileConfig.VpnServerPort;
            existingConfig.ConfigTemplate = openVpnServerOvpnFileConfig.ConfigTemplate;
            existingConfig.LastUpdate = DateTimeOffset.UtcNow;

            await openVpnServerOvpnFileConfigCommandService.Update(existingConfig, true, ct);
        }
        else
        {
            openVpnServerOvpnFileConfig.CreateDate = DateTimeOffset.UtcNow;
            openVpnServerOvpnFileConfig.LastUpdate = DateTimeOffset.UtcNow;

            await openVpnServerOvpnFileConfigCommandService.Add(openVpnServerOvpnFileConfig, true, ct);
        }

        return await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
                   openVpnServerOvpnFileConfig.VpnServerId, ct)
               ?? throw new InvalidOperationException($"OpenVPN server OVPN file configuration not found for " +
                                                      $"server ID {openVpnServerOvpnFileConfig.VpnServerId}.");
    }
}
