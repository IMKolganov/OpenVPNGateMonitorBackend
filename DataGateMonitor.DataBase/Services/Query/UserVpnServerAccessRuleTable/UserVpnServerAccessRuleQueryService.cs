using System.Linq.Expressions;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;

public class UserVpnServerAccessRuleQueryService(
    IQueryService<UserVpnServerAccessRule, int> q) : IUserVpnServerAccessRuleQueryService
{
    public async Task<UserVpnServerAccessOverrides> GetOverridesByUserId(int userId, CancellationToken ct)
    {
        var rules = await q.Where(x => x.UserId == userId, ct: ct);
        if (rules.Count == 0)
            return UserVpnServerAccessOverrides.None;

        var allowed = rules
            .Where(x => x.Mode == VpnServerAccessRuleMode.Allow)
            .Select(x => x.VpnServerId)
            .ToHashSet();
        var denied = rules
            .Where(x => x.Mode == VpnServerAccessRuleMode.Deny)
            .Select(x => x.VpnServerId)
            .ToHashSet();

        return new UserVpnServerAccessOverrides(allowed, denied);
    }

    public Task<UserVpnServerAccessRule?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<UserVpnServerAccessRule?> GetByUserIdAndServerId(int userId, int vpnServerId, CancellationToken ct)
        => q.FirstOrDefault(
            predicate: x => x.UserId == userId && x.VpnServerId == vpnServerId,
            orderBy: s => s.OrderBy(x => x.Id),
            asNoTracking: true,
            ct: ct);

    public Task<List<UserVpnServerAccessRule>> GetListByUserId(int userId, CancellationToken ct)
        => q.Where(x => x.UserId == userId, ct: ct);

    public Task<List<UserVpnServerAccessRule>> GetListByVpnServerId(int vpnServerId, CancellationToken ct)
        => q.Where(x => x.VpnServerId == vpnServerId, ct: ct);

    public Task<IPagedResult<UserVpnServerAccessRule>> GetPage(int page, int pageSize, int? userId, int? vpnServerId,
        CancellationToken ct)
    {
        Expression<Func<UserVpnServerAccessRule, bool>>? predicate = null;
        if (userId is > 0 && vpnServerId is > 0)
            predicate = x => x.UserId == userId && x.VpnServerId == vpnServerId;
        else if (userId is > 0)
            predicate = x => x.UserId == userId;
        else if (vpnServerId is > 0)
            predicate = x => x.VpnServerId == vpnServerId;

        return q.Page(page, pageSize, predicate: predicate, orderBy: s => s.OrderBy(x => x.Id), ct: ct);
    }
}
