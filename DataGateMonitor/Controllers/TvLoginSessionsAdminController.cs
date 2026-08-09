using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/admin/tv-login-sessions")]
[Authorize(Roles = "Admin")]
public sealed class TvLoginSessionsAdminController(ITvLoginAdminService adminService) : BaseController
{
    /// <summary>List TV device-linking sessions (newest first). Optional filters: approvedUserId, status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GetAdminTvLoginSessionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GetAdminTvLoginSessionsResponse>>> List(
        [FromQuery] int? approvedUserId,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await adminService.ListAsync(approvedUserId, status, skip, take, ct);
        return Ok(ApiResponse<GetAdminTvLoginSessionsResponse>.SuccessResponse(result));
    }

    /// <summary>Per-user TV login usage summary for the user detail card.</summary>
    [HttpGet("by-user/{userId:int}/summary")]
    [ProducesResponseType(typeof(ApiResponse<UserTvLoginSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserTvLoginSummaryResponse>>> GetUserSummary(
        [FromRoute] int userId,
        CancellationToken ct)
    {
        var result = await adminService.GetUserSummaryAsync(userId, ct);
        return Ok(ApiResponse<UserTvLoginSummaryResponse>.SuccessResponse(result));
    }
}
