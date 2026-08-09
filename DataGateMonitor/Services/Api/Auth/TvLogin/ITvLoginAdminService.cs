using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

namespace DataGateMonitor.Services.Api.Auth.TvLogin;

public interface ITvLoginAdminService
{
    Task<GetAdminTvLoginSessionsResponse> ListAsync(
        int? approvedUserId,
        string? status,
        int skip,
        int take,
        CancellationToken ct);

    Task<UserTvLoginSummaryResponse> GetUserSummaryAsync(int userId, CancellationToken ct);
}
