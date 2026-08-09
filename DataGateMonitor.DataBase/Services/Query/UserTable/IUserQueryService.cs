using System.Linq.Expressions;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserTable;

public interface IUserQueryService
{
    Task<List<User>> GetAll(CancellationToken ct);
    Task<User?> GetByEmail(string email, CancellationToken ct);
    Task<bool> AnyByEmail(string email, CancellationToken ct);
    Task<User?> GetById(int id, CancellationToken ct);
    Task<User?> GetByExternalId(string externalId, CancellationToken ct);

    /// <summary>
    /// Batch lookup by external id (one links query + one users query).
    /// When multiple links share an external id, the lowest link Id wins (stable).
    /// </summary>
    Task<IReadOnlyDictionary<string, User>> GetByExternalIds(
        IReadOnlyCollection<string> externalIds,
        CancellationToken ct);

    Task<IPagedResult<User>> GetPage(int page, int pageSize, CancellationToken ct);
    Task<IPagedResult<User>> GetPage(GetAllUsersRequest request, CancellationToken ct);
    public Task<List<User>> Search(
        Expression<Func<User, bool>> predicate,
        CancellationToken ct);

    /// <summary>Dashboard users with a non-empty <see cref="User.Email"/> (for admin broadcast).</summary>
    Task<List<User>> GetUsersWithNonEmptyEmailAsync(CancellationToken ct);
}