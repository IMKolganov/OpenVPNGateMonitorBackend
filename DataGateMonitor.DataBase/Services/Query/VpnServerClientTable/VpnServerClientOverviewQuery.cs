using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

public class VpnServerClientOverviewQuery(
    IUnitOfWork uow, IUserQueryService userQueryService) : IVpnServerClientOverviewQuery
{
    // Reusable DB-side projection to DTO
    private static readonly Expression<Func<VpnServerClient, VpnClientInfoDto>> VpnClientSelect =
        x => new VpnClientInfoDto
        {
            Id = x.Id,
            VpnServerId = x.VpnServerId,
            ExternalId = x.ExternalId,
            SessionId = x.SessionId,
            CommonName = x.CommonName,
            RemoteIp = x.RemoteIp,
            ProxyRealIp = x.ProxyRealIp,
            LocalIp = x.LocalIp,
            BytesReceived = x.BytesReceived,
            BytesSent = x.BytesSent,
            ConnectedSince = x.ConnectedSince,
            Username = x.Username,
            Country = x.Country,
            Region = x.Region,
            City = x.City,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            IsConnected = x.IsConnected,
            DisplayName = string.Empty,
            AvatarUrl = null,
        };

    // Connected clients page
    public async Task<VpnClientInfoResponseList> GetAllConnectedVpnServerClientsAsync(
        GetConnectedClientsRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var q = uow.GetQuery<VpnServerClient>()
            .AsQueryable()
            .Where(x => x.VpnServerId == request.VpnServerId && x.IsConnected);

        q = VpnClientListFilterApplier.Apply(q, request);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(VpnClientSelect)
            .AsNoTracking()
            .ToListAsync(ct);

        await EnrichWithUsersAsync(items, ct);

        return new VpnClientInfoResponseList
        {
            VpnClientInfoResponse = items,
            TotalCount = total
        };
    }

    // Full history page + Telegram enrichment
    public async Task<VpnClientInfoResponseList> GetAllHistoryVpnServerClientsAsync(
        GetHistoryClientsRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var q = uow.GetQuery<VpnServerClient>()
            .AsQueryable()
            .Where(x => x.VpnServerId == request.VpnServerId);

        q = VpnClientListFilterApplier.Apply(q, request);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(VpnClientSelect)
            .AsNoTracking()
            .ToListAsync(ct);

        await EnrichWithUsersAsync(items, ct);

        return new VpnClientInfoResponseList
        {
            VpnClientInfoResponse = items,
            TotalCount = total
        };
    }

    // Helper: enrich page items with User.DisplayName by ExternalId (batched)
    private async Task EnrichWithUsersAsync(List<VpnClientInfoDto> items, CancellationToken ct)
    {
        var externalIds = items
            .Select(c => c.ExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (externalIds.Count == 0)
            return;

        var users = await userQueryService.GetByExternalIds(externalIds, ct);

        foreach (var client in items)
        {
            if (string.IsNullOrWhiteSpace(client.ExternalId))
                continue;

            if (users.TryGetValue(client.ExternalId, out var user))
            {
                client.DisplayName = user.DisplayName;
                client.AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl) ? null : user.AvatarUrl;
            }
        }
    }
}