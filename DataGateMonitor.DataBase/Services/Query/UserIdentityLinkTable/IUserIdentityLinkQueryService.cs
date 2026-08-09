using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;

public interface IUserIdentityLinkQueryService
{
    Task<List<UserIdentityLink>> GetAll(CancellationToken ct);
    Task<UserIdentityLink?> GetById(int id, CancellationToken ct);
    Task<UserIdentityLink?> GetByProviderAndExternalId(string provider, string externalId, CancellationToken ct);
    public Task<UserIdentityLink?> GetByExternalId(string externalId, CancellationToken ct);
    Task<UserIdentityLink?> GetByUserId(int userId, CancellationToken ct);
    /// <summary>
    /// First identity link per user (lowest <see cref="UserIdentityLink.Id"/>), matching <see cref="GetByUserId"/>.
    /// </summary>
    Task<IReadOnlyDictionary<int, UserIdentityLink>> GetFirstByUserIds(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct);
    Task<List<UserIdentityLink>> GetListByUserId(int userId, CancellationToken ct);
    Task<bool> AnyByUserId(int userId, CancellationToken ct);

    Task<IPagedResult<UserIdentityLink>> GetPage(int page, int pageSize, CancellationToken ct);
}