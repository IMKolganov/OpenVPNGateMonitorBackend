using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.Api;

public class VpnServerOvpnFileConfigService(
    ILogger<VpnServerOvpnFileConfigService> logger,
    IVpnServerOvpnFileConfigQueryService openVpnServerOvpnFileConfigQueryService,
    ICommandService<VpnServerOvpnFileConfig, int> openVpnServerOvpnFileConfigCommandService,
    IVpnServerQueryService vpnServerQueryService,
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
        var config = await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(vpnServerId, ct)
                     ?? throw new InvalidOperationException("OvpnFileConfig not found");

        await TryApplyResolvedXrayExportEndpointAsync(config, ct);
        return config;
    }

    public async Task<string?> ResolveXrayExportEndpointHostAsync(int vpnServerId, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(vpnServerId, ct);
        if (server is null || server.ServerType != VpnServerType.Xray)
            return null;

        return await ResolveXrayExportEndpointHostAsync(vpnServerId, server.ApiUrl, ct);
    }

    public async Task<VpnServerOvpnFileConfig> AddOrUpdateVpnServerOvpnFileConfigByServerId(
        VpnServerOvpnFileConfig openVpnServerOvpnFileConfig, bool autoDetectServerSettings, CancellationToken ct)
    {
        openVpnServerOvpnFileConfig.VpnServerIp =
            VpnServerApiUrlHelper.SanitizeExportEndpointHost(openVpnServerOvpnFileConfig.VpnServerIp);

        await TryApplyResolvedXrayExportEndpointAsync(openVpnServerOvpnFileConfig, ct);

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

    private async Task TryApplyResolvedXrayExportEndpointAsync(VpnServerOvpnFileConfig config, CancellationToken ct)
    {
        var server = await vpnServerQueryService.GetById(config.VpnServerId, ct);
        if (server is null || server.ServerType != VpnServerType.Xray)
            return;

        config.VpnServerIp = VpnServerApiUrlHelper.SanitizeExportEndpointHost(config.VpnServerIp);
        if (!string.IsNullOrWhiteSpace(config.VpnServerIp))
            return;

        var resolved = await ResolveXrayExportEndpointHostAsync(config.VpnServerId, server.ApiUrl, ct);
        if (!string.IsNullOrWhiteSpace(resolved))
            config.VpnServerIp = resolved;
    }

    private async Task<string?> ResolveXrayExportEndpointHostAsync(int vpnServerId, string? apiUrl, CancellationToken ct)
    {
        string? nodePublicIp = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_openVpnAutoDetectTimeout);
            var diagnostics = await microserviceInfoService.GetInfoAsync(vpnServerId, timeoutCts.Token);
            if (diagnostics?.ServerType == VpnServerType.Xray)
                nodePublicIp = diagnostics.Xray?.PublicIp;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load Xray PublicIp for export endpoint VpnServerId={VpnServerId}.", vpnServerId);
        }

        var resolved = VpnServerApiUrlHelper.ResolveReportedRemoteIp(nodePublicIp, null, apiUrl);
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }
}
