using Microsoft.EntityFrameworkCore;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;

public class UserIdentityLinkQueryService(IQueryService<UserIdentityLink, int> q) : IUserIdentityLinkQueryService
{
    public Task<List<UserIdentityLink>> GetAll(CancellationToken ct)
        => q.GetAll(ct: ct);

    public Task<UserIdentityLink?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<UserIdentityLink?> GetByProviderAndExternalId(string provider, string externalId, 
        CancellationToken ct)
        => q.Query()
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ExternalId == externalId, ct);
    
    public Task<UserIdentityLink?> GetByExternalId(string externalId, CancellationToken ct)
        => q.Query()
            .FirstOrDefaultAsync(x => x.ExternalId == externalId, ct);

    public Task<UserIdentityLink?> GetByUserId(int userId, CancellationToken ct)
        => q.Query()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, UserIdentityLink>> GetFirstByUserIds(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct)
    {
        if (userIds.Count == 0)
            return new Dictionary<int, UserIdentityLink>();

        var links = await q.Query()
            .Where(x => userIds.Contains(x.UserId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        return links
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public Task<List<UserIdentityLink>> GetListByUserId(int userId, CancellationToken ct)
        => q.Query()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public Task<bool> AnyByUserId(int userId, CancellationToken ct)
        => q.Any(x => x.UserId == userId, ct: ct);

    public Task<IPagedResult<UserIdentityLink>> GetPage(int page, int pageSize, CancellationToken ct)
        => q.Page(page, pageSize, ct: ct);
}