using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;

public interface IUserVpnServerAccessRuleQueryService
{
    /// <summary>Personal grants and blocks for the user, grouped by mode.</summary>
    Task<UserVpnServerAccessOverrides> GetOverridesByUserId(int userId, CancellationToken ct);

    Task<UserVpnServerAccessRule?> GetById(int id, CancellationToken ct);
    Task<UserVpnServerAccessRule?> GetByUserIdAndServerId(int userId, int vpnServerId, CancellationToken ct);
    Task<List<UserVpnServerAccessRule>> GetListByUserId(int userId, CancellationToken ct);
    Task<List<UserVpnServerAccessRule>> GetListByVpnServerId(int vpnServerId, CancellationToken ct);
    Task<IPagedResult<UserVpnServerAccessRule>> GetPage(int page, int pageSize, int? userId, int? vpnServerId,
        CancellationToken ct);
}
