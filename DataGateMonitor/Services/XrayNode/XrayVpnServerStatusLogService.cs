using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.XrayNode;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.XrayNode;

public sealed class XrayVpnServerStatusLogService(
    ILogger<XrayVpnServerStatusLogService> logger,
    IVpnServerOvpnFileConfigQueryService vpnServerOvpnFileConfigQueryService,
    IVpnNodePublicIpLookup vpnNodePublicIpLookup,
    IVpnServerStatusLogQueryService vpnServerStatusLogQueryService,
    ICommandService<VpnServerStatusLog, int> vpnServerStatusLogCommandService) : IXrayVpnServerStatusLogService
{
    private static readonly DateTimeOffset StatusLogAnchorTime = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task TryAppendOrUpdateAsync(VpnServer server, XrayNodeClientsResponse payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await AppendOrUpdateCoreAsync(server, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Xray VpnServerStatusLog write failed for server {ServerId} ({Name}); clients sync still applied.",
                server.Id, server.ServerName);
        }
    }

    private async Task AppendOrUpdateCoreAsync(VpnServer server, XrayNodeClientsResponse payload,
        CancellationToken cancellationToken)
    {
        var snap = payload.Server;
        var clients = payload.Clients;

        long bytesIn = snap?.BytesIn ?? clients.Sum(c => c.BytesReceived);
        long bytesOut = snap?.BytesOut ?? clients.Sum(c => c.BytesSent);

        DateTimeOffset upSince;
        if (snap?.UpSince is { } uFromSnap)
            upSince = uFromSnap;
        else if (clients.Count > 0)
            upSince = clients.Min(c => c.ConnectedSince);
        else
            upSince = DateTimeOffset.UtcNow;

        var version = string.IsNullOrWhiteSpace(snap?.Version)
            ? "xray"
            : snap!.Version.Trim();
        if (version.Length > 255)
            version = version[..255];

        var localIp = string.IsNullOrWhiteSpace(snap?.ServerLocalIp)
            ? "-"
            : snap!.ServerLocalIp.Trim();
        if (localIp.Length > 255)
            localIp = localIp[..255];

        var ovpnConfig = await vpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
            server.Id, cancellationToken);
        var nodePublicIp = await vpnNodePublicIpLookup.GetAsync(
            server.Id, VpnServerType.Xray, cancellationToken);
        var remoteIp = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
            nodePublicIp,
            ovpnConfig?.VpnServerIp,
            server.ApiUrl);
        if (string.IsNullOrEmpty(remoteIp))
            remoteIp = "-";

        var sessionId = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(
            $"{server.Id}", "xray-node-status", StatusLogAnchorTime);

        var existing =
            await vpnServerStatusLogQueryService.GetBySessionIdAndVpnServerId(sessionId, server.Id,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.VpnServerId = server.Id;
            existing.UpSince = upSince;
            existing.ServerLocalIp = localIp;
            existing.ServerRemoteIp = remoteIp;
            existing.BytesIn = bytesIn;
            existing.BytesOut = bytesOut;
            existing.Version = version;
            existing.LastUpdate = now;

            await vpnServerStatusLogCommandService.Update(existing, true, cancellationToken);
            logger.LogDebug("Updated Xray VpnServerStatusLog session {SessionId} for server {ServerId}.",
                sessionId, server.Id);
        }
        else
        {
            var row = new VpnServerStatusLog
            {
                VpnServerId = server.Id,
                SessionId = sessionId,
                UpSince = upSince,
                ServerLocalIp = localIp,
                ServerRemoteIp = remoteIp,
                BytesIn = bytesIn,
                BytesOut = bytesOut,
                Version = version,
                LastUpdate = now,
                CreateDate = now,
            };

            await vpnServerStatusLogCommandService.Add(row, true, cancellationToken);
            logger.LogDebug("Created Xray VpnServerStatusLog session {SessionId} for server {ServerId}.",
                sessionId, server.Id);
        }
    }
}
