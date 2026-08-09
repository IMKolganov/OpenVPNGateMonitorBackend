using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;

public interface ITvLoginSessionQueryService
{
    Task<TvLoginSession?> GetById(Guid id, CancellationToken ct);
    Task<TvLoginSession?> GetActiveByUserCode(string normalizedUserCode, CancellationToken ct);
    Task<TvLoginSession?> GetLatestByUserCode(string normalizedUserCode, CancellationToken ct);
    Task<bool> AnyActiveByUserCode(string normalizedUserCode, CancellationToken ct);

    Task<(IReadOnlyList<TvLoginSession> Items, int TotalCount)> ListAsync(
        int? approvedUserId,
        TvLoginSessionStatus? status,
        int skip,
        int take,
        CancellationToken ct);

    Task<TvLoginSession?> GetLatestApprovedOrConsumedForUserAsync(int userId, CancellationToken ct);

    Task<int> CountApprovedOrConsumedForUserAsync(int userId, CancellationToken ct);
}
