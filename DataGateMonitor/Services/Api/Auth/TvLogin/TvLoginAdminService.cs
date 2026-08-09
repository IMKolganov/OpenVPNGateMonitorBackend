using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

namespace DataGateMonitor.Services.Api.Auth.TvLogin;

public sealed class TvLoginAdminService(
    ITvLoginSessionQueryService sessions,
    IUserQueryService users) : ITvLoginAdminService
{
    public async Task<GetAdminTvLoginSessionsResponse> ListAsync(
        int? approvedUserId,
        string? status,
        int skip,
        int take,
        CancellationToken ct)
    {
        TvLoginSessionStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
                throw new InvalidOperationException($"Unknown TV login status '{status}'.");
            statusFilter = parsed;
        }

        var (items, total) = await sessions.ListAsync(approvedUserId, statusFilter, skip, take, ct);

        var userIds = items
            .Where(x => x.ApprovedUserId is int)
            .Select(x => x.ApprovedUserId!.Value)
            .Distinct()
            .ToList();

        var userMap = new Dictionary<int, User>();
        foreach (var id in userIds)
        {
            var user = await users.GetById(id, ct);
            if (user != null)
                userMap[id] = user;
        }

        var now = DateTimeOffset.UtcNow;
        var dtos = items.Select(s =>
        {
            userMap.TryGetValue(s.ApprovedUserId ?? -1, out var user);
            return new AdminTvLoginSessionDto
            {
                SessionId = s.Id,
                UserCode = s.UserCode,
                Status = MapStatus(s, now),
                DeviceName = s.DeviceName,
                Client = s.Client,
                DeviceId = s.DeviceId,
                UserAgent = s.UserAgent,
                ApprovedUserId = s.ApprovedUserId,
                ApprovedUserEmail = user?.Email,
                ApprovedUserDisplayName = user?.DisplayName,
                CreateDate = s.CreateDate,
                ExpiresAt = s.ExpiresAt,
                CompletedAt = s.CompletedAt,
            };
        }).ToList();

        return new GetAdminTvLoginSessionsResponse
        {
            Sessions = dtos,
            TotalCount = total,
        };
    }

    public async Task<UserTvLoginSummaryResponse> GetUserSummaryAsync(int userId, CancellationToken ct)
    {
        var count = await sessions.CountApprovedOrConsumedForUserAsync(userId, ct);
        var latest = count > 0
            ? await sessions.GetLatestApprovedOrConsumedForUserAsync(userId, ct)
            : null;

        return new UserTvLoginSummaryResponse
        {
            HasUsedTvLogin = count > 0,
            ApprovedOrConsumedCount = count,
            LastUsedAt = latest?.CompletedAt ?? latest?.CreateDate,
            LastDeviceName = latest?.DeviceName,
            LastClient = latest?.Client,
        };
    }

    internal static string MapStatus(TvLoginSession session, DateTimeOffset now)
    {
        if ((session.Status == TvLoginSessionStatus.Pending || session.Status == TvLoginSessionStatus.Viewed)
            && session.ExpiresAt <= now)
        {
            return "expired";
        }

        return session.Status switch
        {
            TvLoginSessionStatus.Pending => "pending",
            TvLoginSessionStatus.Viewed => "viewed",
            TvLoginSessionStatus.Approved => "approved",
            TvLoginSessionStatus.Denied => "denied",
            TvLoginSessionStatus.Expired => "expired",
            TvLoginSessionStatus.Consumed => "consumed",
            _ => "expired",
        };
    }

    private static bool TryParseStatus(string status, out TvLoginSessionStatus parsed)
    {
        switch (status.Trim().ToLowerInvariant())
        {
            case "pending":
                parsed = TvLoginSessionStatus.Pending;
                return true;
            case "viewed":
                parsed = TvLoginSessionStatus.Viewed;
                return true;
            case "approved":
                parsed = TvLoginSessionStatus.Approved;
                return true;
            case "denied":
                parsed = TvLoginSessionStatus.Denied;
                return true;
            case "expired":
                parsed = TvLoginSessionStatus.Expired;
                return true;
            case "consumed":
                parsed = TvLoginSessionStatus.Consumed;
                return true;
            default:
                parsed = default;
                return false;
        }
    }
}
