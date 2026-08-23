using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.Models;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.VpnManagerReleases;

public interface IVpnServerManagerVersionPersister
{
    /// <summary>
    /// Fetches node <c>/api/info</c> and stores the manager assembly version on <see cref="VpnServer.ManagerVersion"/>.
    /// Failures are logged and swallowed so callers (background poll) are not disrupted.
    /// </summary>
    Task TryPersistAsync(VpnServer server, CancellationToken cancellationToken);
}

public sealed class VpnServerManagerVersionPersister(
    IMicroserviceInfoService microserviceInfoService,
    ICommandService<VpnServer, int> vpnServerCommandService,
    ILogger<VpnServerManagerVersionPersister> logger) : IVpnServerManagerVersionPersister
{
    private const int MaxVersionLength = 64;

    public async Task TryPersistAsync(VpnServer server, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(server.ApiUrl))
                return;

            var info = await microserviceInfoService.GetInfoAsync(server.Id, cancellationToken);
            var version = info.ServerType == VpnServerType.Xray
                ? info.Xray?.Version
                : info.OpenVpn?.Version;

            if (string.IsNullOrWhiteSpace(version))
                return;

            var trimmed = version.Trim();
            if (trimmed.Length > MaxVersionLength)
                trimmed = trimmed[..MaxVersionLength];

            if (string.Equals(server.ManagerVersion, trimmed, StringComparison.Ordinal))
                return;

            var now = DateTimeOffset.UtcNow;
            await vpnServerCommandService.UpdateWhere(
                s => s.Id == server.Id,
                u => u.SetProperty(x => x.ManagerVersion, trimmed)
                    .SetProperty(x => x.LastUpdate, now),
                cancellationToken);

            server.ManagerVersion = trimmed;
            server.LastUpdate = now;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Could not persist manager version for VpnServerId={VpnServerId} ({Name})",
                server.Id, server.ServerName);
        }
    }
}
