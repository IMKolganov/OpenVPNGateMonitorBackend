using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;

public interface IVpnServerOvpnFileConfigQueryService
{
    Task<List<VpnServerOvpnFileConfig>> GetAll(CancellationToken ct);
    Task<VpnServerOvpnFileConfig?> GetById(int id, CancellationToken ct);
    Task<VpnServerOvpnFileConfig?> GetByVpnServerIdId(int vpnServerId, CancellationToken ct);
    Task<Dictionary<int, string>> GetConfigTemplatesByVpnServerIds(
        IReadOnlyCollection<int> vpnServerIds,
        CancellationToken ct);
    Task<bool> AnyByVpnServerId(int vpnServerId, CancellationToken ct);
    Task<IPagedResult<VpnServerOvpnFileConfig>> GetPage(int page, int pageSize, CancellationToken ct);
}