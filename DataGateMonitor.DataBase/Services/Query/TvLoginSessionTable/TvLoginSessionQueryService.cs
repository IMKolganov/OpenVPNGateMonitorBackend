using Microsoft.EntityFrameworkCore;
using DataGateMonitor.Models;

namespace DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;

public class TvLoginSessionQueryService(IQueryService<TvLoginSession, Guid> q) : ITvLoginSessionQueryService
{
    public Task<TvLoginSession?> GetById(Guid id, CancellationToken ct)
        => q.FindById(id, ct: ct);

    public Task<TvLoginSession?> GetActiveByUserCode(string normalizedUserCode, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return q.Query()
            .Where(x =>
                x.UserCode == normalizedUserCode
                && (x.Status == TvLoginSessionStatus.Pending || x.Status == TvLoginSessionStatus.Viewed)
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreateDate)
            .FirstOrDefaultAsync(ct);
    }

    public Task<TvLoginSession?> GetLatestByUserCode(string normalizedUserCode, CancellationToken ct)
        => q.Query()
            .Where(x => x.UserCode == normalizedUserCode)
            .OrderByDescending(x => x.CreateDate)
            .FirstOrDefaultAsync(ct);

    public Task<bool> AnyActiveByUserCode(string normalizedUserCode, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return q.Query()
            .AnyAsync(
                x =>
                    x.UserCode == normalizedUserCode
                    && (x.Status == TvLoginSessionStatus.Pending || x.Status == TvLoginSessionStatus.Viewed)
                    && x.ExpiresAt > now,
                ct);
    }

    public async Task<(IReadOnlyList<TvLoginSession> Items, int TotalCount)> ListAsync(
        int? approvedUserId,
        TvLoginSessionStatus? status,
        int skip,
        int take,
        CancellationToken ct)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var query = q.Query().AsNoTracking();
        if (approvedUserId is int userId)
            query = query.Where(x => x.ApprovedUserId == userId);
        if (status is TvLoginSessionStatus st)
            query = query.Where(x => x.Status == st);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreateDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<TvLoginSession?> GetLatestApprovedOrConsumedForUserAsync(int userId, CancellationToken ct)
        => q.Query()
            .AsNoTracking()
            .Where(x =>
                x.ApprovedUserId == userId
                && (x.Status == TvLoginSessionStatus.Approved || x.Status == TvLoginSessionStatus.Consumed))
            .OrderByDescending(x => x.CompletedAt ?? x.CreateDate)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountApprovedOrConsumedForUserAsync(int userId, CancellationToken ct)
        => q.Query()
            .CountAsync(
                x =>
                    x.ApprovedUserId == userId
                    && (x.Status == TvLoginSessionStatus.Approved || x.Status == TvLoginSessionStatus.Consumed),
                ct);
}
