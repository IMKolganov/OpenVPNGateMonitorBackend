using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;

public class VpnServerGroupQueryService(IQueryService<VpnServerGroup, int> q) : IVpnServerGroupQueryService
{
    public Task<List<VpnServerGroup>> GetAll(CancellationToken ct)
        => q.Where(
            predicate: _ => true,
            orderBy: s => s.OrderBy(x => x.SortOrder).ThenBy(x => x.Id),
            ct: ct);

    public Task<VpnServerGroup?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<VpnServerGroup?> GetByName(string name, CancellationToken ct)
        => q.FirstOrDefault(
            predicate: x => x.Name == name,
            orderBy: s => s.OrderBy(x => x.Id),
            ct: ct);

    public Task<IPagedResult<VpnServerGroup>> GetPage(int page, int pageSize, CancellationToken ct)
        => q.Page(
            page,
            pageSize,
            orderBy: s => s.OrderBy(x => x.SortOrder).ThenBy(x => x.Id),
            ct: ct);
}
