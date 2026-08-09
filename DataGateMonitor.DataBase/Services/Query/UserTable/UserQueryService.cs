using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using DataGateMonitor.DataBase.UnitOfWork;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserTable;

public class UserQueryService(
    IQueryService<User, int> q,
    IQueryService<UserIdentityLink, int> qUserIdentityLink,
    IUnitOfWork uow
) : IUserQueryService
{
    public Task<List<User>> GetAll(CancellationToken ct)
        => q.GetAll(ct: ct);

    public Task<User?> GetById(int id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public async Task<IReadOnlyDictionary<int, User>> GetByIds(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct)
    {
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, User>();

        var users = await q.Where(
            predicate: x => ids.Contains(x.Id),
            asNoTracking: true,
            ct: ct);
        return users.ToDictionary(u => u.Id);
    }

    public async Task<User?> GetByEmail(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.Trim().ToUpperInvariant();
        return await q.FirstOrDefault(
            predicate: u => u.Email != null && u.Email.ToUpper() == normalizedEmail,
            asNoTracking: true,
            ct: ct
        );
    }

    public Task<bool> AnyByEmail(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult(false);

        var normalizedEmail = email.Trim().ToUpperInvariant();
        return q.Any(x => x.Email != null && x.Email.ToUpper() == normalizedEmail, ct: ct);
    }

    public async Task<User?> GetByExternalId(string externalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        var map = await GetByExternalIds([externalId], ct);
        return map.TryGetValue(externalId, out var user) ? user : null;
    }

    public async Task<IReadOnlyDictionary<string, User>> GetByExternalIds(
        IReadOnlyCollection<string> externalIds,
        CancellationToken ct)
    {
        var ids = externalIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return new Dictionary<string, User>(StringComparer.Ordinal);

        var links = await qUserIdentityLink.Where(
            predicate: x => ids.Contains(x.ExternalId),
            orderBy: qy => qy.OrderBy(x => x.Id),
            asNoTracking: true,
            ct: ct);

        var firstLinkByExternalId = new Dictionary<string, UserIdentityLink>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            if (!firstLinkByExternalId.ContainsKey(link.ExternalId))
                firstLinkByExternalId[link.ExternalId] = link;
        }

        if (firstLinkByExternalId.Count == 0)
            return new Dictionary<string, User>(StringComparer.Ordinal);

        var userIds = firstLinkByExternalId.Values.Select(l => l.UserId).Distinct().ToList();
        var users = await q.Where(
            predicate: x => userIds.Contains(x.Id),
            asNoTracking: true,
            ct: ct);
        var usersById = users.ToDictionary(u => u.Id);

        var result = new Dictionary<string, User>(StringComparer.Ordinal);
        foreach (var (externalId, link) in firstLinkByExternalId)
        {
            if (usersById.TryGetValue(link.UserId, out var user))
                result[externalId] = user;
        }

        return result;
    }

    public Task<IPagedResult<User>> GetPage(int page, int pageSize, CancellationToken ct)
        => GetPage(new GetAllUsersRequest { Page = page, PageSize = pageSize }, ct);

    public async Task<IPagedResult<User>> GetPage(GetAllUsersRequest request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
        if (pageSize > 500)
            pageSize = 500;

        var query = uow.GetQuery<User>().AsQueryable();

        var searchPattern = GridFilterHelper.ContainsPattern(request.Search);
        if (searchPattern != null)
        {
            query = query.Where(u =>
                EF.Functions.ILike(u.DisplayName, searchPattern) ||
                (u.Email != null && EF.Functions.ILike(u.Email, searchPattern)));
        }

        var externalIdPattern = GridFilterHelper.ContainsPattern(request.ExternalId);
        var providerPattern = GridFilterHelper.ContainsPattern(request.Provider);
        if (externalIdPattern != null || providerPattern != null)
        {
            var links = uow.GetQuery<UserIdentityLink>().AsQueryable();
            if (externalIdPattern != null)
                links = links.Where(l => EF.Functions.ILike(l.ExternalId, externalIdPattern));
            if (providerPattern != null)
                links = links.Where(l => EF.Functions.ILike(l.Provider, providerPattern));

            var userIds = links.Select(l => l.UserId);
            query = query.Where(u => userIds.Contains(u.Id));
        }

        if (request.IsAdmin.HasValue)
            query = query.Where(u => u.IsAdmin == request.IsAdmin.Value);

        if (request.IsBlocked.HasValue)
            query = query.Where(u => u.IsBlocked == request.IsBlocked.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResponse<User>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        };
    }

    public Task<List<User>> Search(
        Expression<Func<User, bool>> predicate,
        CancellationToken ct)
        => q.Where(predicate, ct: ct);

    public Task<List<User>> GetUsersWithNonEmptyEmailAsync(CancellationToken ct) =>
        q.Where(
            predicate: u => u.Email != null && u.Email != "",
            orderBy: x => x.OrderBy(u => u.Id),
            asNoTracking: true,
            ct: ct);
}