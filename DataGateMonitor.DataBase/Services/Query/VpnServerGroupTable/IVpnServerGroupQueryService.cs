using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;

public interface IVpnServerGroupQueryService
{
    Task<List<VpnServerGroup>> GetAll(CancellationToken ct);
    Task<VpnServerGroup?> GetById(int id, CancellationToken ct);
    Task<VpnServerGroup?> GetByName(string name, CancellationToken ct);
    Task<IPagedResult<VpnServerGroup>> GetPage(int page, int pageSize, CancellationToken ct);
}
