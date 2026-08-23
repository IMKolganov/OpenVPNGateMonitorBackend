using Microsoft.EntityFrameworkCore;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;

public class VpnServerOvpnFileConfigQueryService(
    IQueryService<VpnServerOvpnFileConfig, int> q) : IVpnServerOvpnFileConfigQueryService
{
    public Task<List<VpnServerOvpnFileConfig>> GetAll(CancellationToken ct)
        => q.GetAll(ct: ct);
    public Task<VpnServerOvpnFileConfig?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);
    public Task<VpnServerOvpnFileConfig?> GetByVpnServerIdId(
        int vpnServerId,
        CancellationToken ct)
        => q.Query()
            .FirstOrDefaultAsync(x => x.VpnServerId == vpnServerId, ct);

    public async Task<Dictionary<int, string>> GetConfigTemplatesByVpnServerIds(
        IReadOnlyCollection<int> vpnServerIds,
        CancellationToken ct)
    {
        if (vpnServerIds.Count == 0)
            return [];

        return await q.Query()
            .Where(x => vpnServerIds.Contains(x.VpnServerId))
            .Select(x => new { x.VpnServerId, x.ConfigTemplate })
            .ToDictionaryAsync(x => x.VpnServerId, x => x.ConfigTemplate, ct);
    }

    public Task<bool> AnyByVpnServerId(int vpnServerId, CancellationToken ct)
        => q.Any(x => x.VpnServerId == vpnServerId, ct: ct);
    public Task<IPagedResult<VpnServerOvpnFileConfig>> GetPage(int page, int pageSize, CancellationToken ct)
        => q.Page(page, pageSize, ct: ct);
}