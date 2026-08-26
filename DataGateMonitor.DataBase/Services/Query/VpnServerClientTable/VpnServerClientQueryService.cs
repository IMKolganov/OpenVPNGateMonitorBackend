using Microsoft.EntityFrameworkCore;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;

public class VpnServerClientQueryService(IQueryService<VpnServerClient, int> q) : IVpnServerClientQueryService
{
    public Task<List<VpnServerClient>> GetAll(CancellationToken ct)
        => q.GetAll(ct: ct);

    public Task<List<VpnServerClient>> GetAllConnectedByVpnServerId(int vpnServerId, CancellationToken ct)
        => q.Where(x => x.IsConnected && x.VpnServerId == vpnServerId, ct: ct);

    public Task<List<VpnServerClient>> GetAllConnected(CancellationToken ct)
        => q.Where(x => x.IsConnected, ct: ct);

    public async Task<IReadOnlyList<int>> GetConnectedVpnServerIdsForUserAsync(
        int? userId,
        string? externalId,
        CancellationToken ct)
    {
        var normalizedExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        if ((userId is null or <= 0) && normalizedExternalId is null)
            return Array.Empty<int>();

        var query = q.Query().Where(x => x.IsConnected);

        if (userId is > 0 && normalizedExternalId is not null)
            query = query.Where(x => x.UserId == userId || x.ExternalId == normalizedExternalId);
        else if (userId is > 0)
            query = query.Where(x => x.UserId == userId);
        else
            query = query.Where(x => x.ExternalId == normalizedExternalId);

        return await query
            .Select(x => x.VpnServerId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<VpnServerClient?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<VpnServerClient?> GetBySessionAndServerId(Guid session, int vpnServerId, CancellationToken ct)
        => q.Query()
            .FirstOrDefaultAsync(x => x.SessionId == session && x.VpnServerId == vpnServerId, ct);

    public Task<IPagedResult<VpnServerClient>> GetPage(int page, int pageSize, CancellationToken ct)
        => q.Page(page, pageSize, ct: ct);
}