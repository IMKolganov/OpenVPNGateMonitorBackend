using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;

namespace DataGateMonitor.Services.Api.Auth.TvLogin;

public interface ITvLoginSessionService
{
    Task<CreateTvLoginSessionResponse> CreateSessionAsync(
        CreateTvLoginSessionRequest request,
        string? clientIp,
        CancellationToken ct);

    Task<TvLoginSessionPollResponse> PollSessionAsync(Guid sessionId, string? clientIp, CancellationToken ct);

    Task<TvLoginSessionPreviewResponse> GetByUserCodeAsync(
        string userCode,
        int requestingUserId,
        string? clientIp,
        CancellationToken ct);

    Task<TvLoginSessionActionResponse> ApproveAsync(
        ApproveTvLoginSessionRequest request,
        int approvingUserId,
        string? clientIp,
        CancellationToken ct);

    Task<TvLoginSessionActionResponse> DenyAsync(
        DenyTvLoginSessionRequest request,
        int denyingUserId,
        string? clientIp,
        CancellationToken ct);
}
