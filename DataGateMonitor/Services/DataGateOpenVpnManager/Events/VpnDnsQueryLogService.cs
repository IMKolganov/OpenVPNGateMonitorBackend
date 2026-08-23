using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.PiHole.Requests;
using Microsoft.EntityFrameworkCore;

namespace DataGateMonitor.Services.DataGateOpenVpnManager.Events;

public class VpnDnsQueryLogService(
    ICommandService<VpnDnsQueryLog, int> cmd,
    IQueryService<VpnDnsQueryLog, int> query,
    IIssuedOvpnFileQueryService issuedOvpnFileQueryService,
    IIssuedXrayClientLinkQueryService issuedXrayClientLinkQueryService,
    ILogger<VpnDnsQueryLogService> logger) : IVpnDnsQueryLogService
{
    public async Task<int> SaveBatchAsync(int vpnServerId, DnsQueryBatchRequest batch, CancellationToken ct)
    {
        if (batch.Queries.Count == 0)
            return 0;

        var piHoleIds = batch.Queries.Select(q => q.PiHoleQueryId).Distinct().ToList();
        var existingIds = await query.Query()
            .Where(x => x.VpnServerId == vpnServerId && piHoleIds.Contains(x.PiHoleQueryId))
            .Select(x => x.PiHoleQueryId)
            .ToListAsync(ct);
        var existingSet = existingIds.ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var rows = new List<VpnDnsQueryLog>();

        foreach (var queryItem in batch.Queries)
        {
            if (existingSet.Contains(queryItem.PiHoleQueryId))
                continue;

            string? externalId = null;
            if (!string.IsNullOrWhiteSpace(queryItem.CommonName))
            {
                externalId = await issuedOvpnFileQueryService.GetExternalIdByCommonName(
                    queryItem.CommonName, vpnServerId, ct);
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    externalId = await issuedXrayClientLinkQueryService.GetExternalIdByCommonName(
                        queryItem.CommonName, vpnServerId, ct);
                }
            }

            rows.Add(new VpnDnsQueryLog
            {
                VpnServerId = vpnServerId,
                PiHoleQueryId = queryItem.PiHoleQueryId,
                CommonName = queryItem.CommonName,
                ExternalId = externalId,
                ClientIp = queryItem.ClientIp,
                Domain = queryItem.Domain,
                QueryType = queryItem.QueryType,
                Status = queryItem.Status,
                QueriedAtUtc = queryItem.QueriedAtUtc,
                CreateDate = now,
                LastUpdate = now
            });
        }

        if (rows.Count == 0)
        {
            logger.LogDebug(
                "Pi-hole DNS batch for VpnServerId={VpnServerId}: all {Total} queries were duplicates.",
                vpnServerId,
                batch.Queries.Count);
            return 0;
        }

        await cmd.AddRange(rows, saveChanges: true, ct);
        var withoutExternalId = rows.Count(r => string.IsNullOrWhiteSpace(r.ExternalId));
        logger.LogInformation(
            "Stored Pi-hole DNS batch for VpnServerId={VpnServerId}: saved={Saved}, received={Received}, duplicates={Duplicates}, missingExternalId={MissingExternalId}",
            vpnServerId,
            rows.Count,
            batch.Queries.Count,
            batch.Queries.Count - rows.Count,
            withoutExternalId);
        return rows.Count;
    }
}
