using DataGateMonitor.DataBase.Services.Command;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Command.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerStatusLogTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Models.Helpers.Services;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager;
using DataGateMonitor.Services.OpenVpnManagementInterfaces;
using DataGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Services.BackgroundServices;

public class VpnServerService(
    ILogger<IVpnServerService> logger,
    IOpenVpnClientService openVpnClientService,
    IOpenVpnSummaryStatService openVpnSummaryStatService,
    IOpenVpnVersionService openVpnVersionService,
    IOpenVpnStateService openVpnStateService,
    IIssuedOvpnFileQueryService openVpnFileQueryService,
    IVpnServerStatusLogQueryService openVpnServerStatusLogQueryService,
    IVpnServerOvpnFileConfigQueryService openVpnServerOvpnFileConfigQueryService,
    IVpnNodePublicIpLookup vpnNodePublicIpLookup,
    ITransactionRunner transactionRunner,
    IUserQueryService userQueryService,
    ICommandService<VpnServer, int> openVpnServerCommandService,
    ICommandService<VpnServerClient, int> openVpnServerClientCommandService,
    ICommandService<VpnServerStatusLog, int> openVpnServerStatusLogCommandService,
    ICommandService<VpnServerClientTraffic, int> openVpnClientTrafficCommandService,
    IVpnServerClientUpsertService vpnServerClientUpsertService,
    IConnectedClientsCounterStore connectedClientsCounterStore) : IVpnServerService
{
    public async Task SaveConnectedClientsAsync(VpnServer openVpnServer, CancellationToken ct)
    {
        logger.LogInformation("VpnServerId: {Id}. Starting SaveConnectedClientsAsync...", openVpnServer.Id);

        await transactionRunner.RunAsync(async _ =>
        {
            var statusResult = await openVpnClientService.GetClientsFromManagementAsync(openVpnServer, ct);
            var openVpnClientsFromMng = statusResult.Clients;
            logger.LogInformation("VpnServerId: {Id}. Retrieved {Count} clients from OpenVPN.",
                openVpnServer.Id, openVpnClientsFromMng.Count);

            if (statusResult.DcoEnabled.HasValue)
            {
                await openVpnServerCommandService.UpdateWhere(
                    s => s.Id == openVpnServer.Id,
                    u => u.SetProperty(x => x.DcoIsEnabled, statusResult.DcoEnabled.Value)
                        .SetProperty(x => x.LastUpdate, DateTimeOffset.UtcNow),
                    ct);
            }

            var nowUtc = DateTimeOffset.UtcNow;
            User? user;

            // Build current sessions set to mark stale ones as disconnected
            var currentSessionIds = new HashSet<Guid>(
                openVpnClientsFromMng.Select(c =>
                    VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(c.CommonName, c.RemoteIp, c.ConnectedSince)));

            // Mark not-present sessions as disconnected
            await openVpnServerClientCommandService.UpdateWhere(
                x => x.VpnServerId == openVpnServer.Id
                     && x.IsConnected
                     && !currentSessionIds.Contains(x.SessionId),
                s => s
                    .SetProperty(c => c.IsConnected, false)
                    .SetProperty(c => c.DisconnectedAt, nowUtc)
                    .SetProperty(c => c.LastUpdate, nowUtc),
                ct);

            // Upsert for each currently connected client + traffic sample
            foreach (var m in openVpnClientsFromMng)
            {
                var sessionId = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(m.CommonName, m.RemoteIp, m.ConnectedSince);
                var externalId = await openVpnFileQueryService.GetExternalIdByCommonName(
                    m.CommonName, openVpnServer.Id, ct) ?? string.Empty;
                user = await userQueryService.GetByExternalId(externalId, ct) ?? null;
                
                var persistProxyEnrichment = ProxyClientLookupService.ShouldPersistProxyEnrichmentFromPoll(m);
                await vpnServerClientUpsertService.UpsertAsync(
                    VpnServerClientUpsertPayload.FromClient(
                        m,
                        persistProxyEnrichment: persistProxyEnrichment,
                        vpnServerId: openVpnServer.Id,
                        userId: user?.Id,
                        externalId: externalId,
                        sessionId: sessionId,
                        isConnected: true),
                    ct);
                logger.LogDebug("Upserted client session {SessionId}.", sessionId);

                // ---- Write traffic sample on every call (exact MeasuredAt)
                var measuredAt = DateTimeOffset.UtcNow; // exact timestamp of this poll

                // Conditional UPDATE to avoid counter regression; then INSERT if not found
                var tRows = await openVpnClientTrafficCommandService.UpdateWhere(
                    x => x.VpnServerId == openVpnServer.Id
                         && x.SessionId == sessionId
                         && x.MeasuredAt == measuredAt
                         && x.BytesReceived <= m.BytesReceived
                         && x.BytesSent <= m.BytesSent,
                    s => s
                        .SetProperty(t => t.ExternalId, externalId)
                        .SetProperty(t => t.BytesReceived, m.BytesReceived)
                        .SetProperty(t => t.BytesSent, m.BytesSent),
                    ct);

                if (tRows == 0)
                {
                    var sample = new VpnServerClientTraffic
                    {
                        VpnServerId = openVpnServer.Id,
                        UserId = user?.Id,
                        ExternalId = externalId,
                        SessionId = sessionId,
                        BytesReceived = m.BytesReceived,
                        BytesSent = m.BytesSent,
                        MeasuredAt = measuredAt // saved as UTC in setter
                    };

                    await openVpnClientTrafficCommandService.Add(sample, saveChanges: false, ct);
                }
            }

            // Persist both sets (clients + traffic)
            await openVpnServerClientCommandService.SaveChanges(ct);
            await openVpnClientTrafficCommandService.SaveChanges(ct);
            await connectedClientsCounterStore.SetAsync(openVpnServer.Id, openVpnClientsFromMng.Count, ct);

            logger.LogInformation("VpnServerId: {Id}. SaveConnectedClientsAsync completed successfully.",
                openVpnServer.Id);

        }, ct);
    }

    public async Task SaveVpnServerStatusLogAsync(VpnServer openVpnServer, CancellationToken ct)
    {
        logger.LogInformation($"VpnServerId: {openVpnServer.Id}. Starting SaveVpnServerStatusLogAsync...");

        var serverInfo = new ServerInfo();
        try
        {
            serverInfo.OpenVpnState = await openVpnStateService.GetStateAsync(openVpnServer, ct);
            if (serverInfo.OpenVpnState.UpSince <= DateTimeOffset.MinValue)
            {
                var state = serverInfo.OpenVpnState;
                var stateDiagnostics =
                    $"Connected={state.Connected}; " +
                    $"Success={state.Success}; " +
                    $"UpSince={state.UpSince:O}; " +
                    $"ServerLocalIp={state.ServerLocalIp ?? "<null>"}; " +
                    $"ServerRemoteIp={state.ServerRemoteIp ?? "<null>"}; " +
                    $"RawStateResponse={OpenVpnStateService.FormatRawResponseForLog(state.RawResponse)}";

                throw new Exception(
                    $"VpnServerId: {openVpnServer.Id}. " +
                    $"UpSince is not set (<= MinValue) after state parsing. " +
                    $"ServerName={openVpnServer.ServerName}; " +
                    $"ApiUrl={openVpnServer.ApiUrl}; " +
                    $"OpenVpnState: {stateDiagnostics}. " +
                    $"Check the OpenVPN management 'state' payload format or server configuration.");
            }

            if (serverInfo.OpenVpnState != null)
            {
                var ovpnConfig = await openVpnServerOvpnFileConfigQueryService.GetByVpnServerIdId(
                    openVpnServer.Id, ct);
                var nodePublicIp = await vpnNodePublicIpLookup.GetAsync(
                    openVpnServer.Id, VpnServerType.OpenVpn, ct);
                serverInfo.OpenVpnState.ServerRemoteIp = VpnServerApiUrlHelper.ResolveReportedRemoteIp(
                    nodePublicIp,
                    ovpnConfig?.VpnServerIp,
                    openVpnServer.ApiUrl);

                serverInfo.Version = await openVpnVersionService.GetVersionAsync(openVpnServer, ct);
            }

            // load-stats returns server-level cumulative bytesin/bytesout.
            // Note: when DCO (Data Channel Offload) is enabled, load-stats may return incorrect values.
            serverInfo.OpenVpnSummaryStats = await openVpnSummaryStatService.GetSummaryStatsAsync(openVpnServer, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"VpnServerId: {openVpnServer.Id}. Failed to get OpenVPN status. " +
                                $"Error: {ex.Message}");
            throw;
        }

        serverInfo.Status = serverInfo.OpenVpnState != null ? "CONNECTED" : "DISCONNECTED";


        if (serverInfo.OpenVpnState == null)
        {
            logger.LogWarning($"VpnServerId: {openVpnServer.Id}. OpenVPN State is null. Cannot proceed.");
            throw new InvalidOperationException(
                $"VpnServerId: {openVpnServer.Id}. OpenVPN State is null. Cannot proceed.");
        }

        var sessionId = VpnSessionIdGenerator.FromCommonNameRemoteConnectedSince(
            $"{openVpnServer.Id}{openVpnServer.ServerName}",
            serverInfo.OpenVpnState.ServerLocalIp ??
            throw new InvalidOperationException($"VpnServerId: {openVpnServer.Id}. LocalIp cannot be null"),
            serverInfo.OpenVpnState.UpSince
        );

        var existingStatusLog =
            await openVpnServerStatusLogQueryService.GetBySessionIdAndVpnServerId(sessionId, openVpnServer.Id, ct);

        if (existingStatusLog != null)
        {
            existingStatusLog.VpnServerId = openVpnServer.Id;
            existingStatusLog.UpSince = serverInfo.OpenVpnState.UpSince;
            existingStatusLog.ServerLocalIp = serverInfo.OpenVpnState.ServerLocalIp;
            existingStatusLog.ServerRemoteIp = serverInfo.OpenVpnState.ServerRemoteIp;
            existingStatusLog.BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0;
            existingStatusLog.BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0;
            existingStatusLog.Version = serverInfo.Version;
            existingStatusLog.LastUpdate = DateTimeOffset.UtcNow;

            await openVpnServerStatusLogCommandService.Update(existingStatusLog, true, ct);
            logger.LogInformation($"VpnServerId: {openVpnServer.Id}. Updated existing status log {sessionId}.");
        }
        else
        {
            var newStatusLog = new VpnServerStatusLog
            {
                VpnServerId = openVpnServer.Id,
                SessionId = sessionId,
                UpSince = serverInfo.OpenVpnState.UpSince,
                ServerLocalIp = serverInfo.OpenVpnState.ServerLocalIp,
                ServerRemoteIp = serverInfo.OpenVpnState.ServerRemoteIp,
                BytesIn = serverInfo.OpenVpnSummaryStats?.BytesIn ?? 0,
                BytesOut = serverInfo.OpenVpnSummaryStats?.BytesOut ?? 0,
                Version = serverInfo.Version,
                LastUpdate = DateTimeOffset.UtcNow,
                CreateDate = DateTimeOffset.UtcNow
            };

            await openVpnServerStatusLogCommandService.Add(newStatusLog, true, ct);
            logger.LogInformation($"VpnServerId: {openVpnServer.Id}. Created new status log {{SessionId}}.", sessionId);
        }

        logger.LogInformation($"VpnServerId: {openVpnServer.Id}. " +
                              $"SaveVpnServerStatusLogAsync completed successfully.");
    }
}