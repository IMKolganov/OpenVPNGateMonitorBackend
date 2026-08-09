using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.FreeTierEnforcement.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

/// <summary>
/// Admin view of free-tier ("unboarding") OpenVPN session enforcement: who currently qualifies for
/// disconnection and the audit log of past kills (automated or manual).
/// </summary>
[ApiController]
[Route("api/free-tier-enforcement")]
[Authorize]
[Authorize(Roles = "Admin,App")]
public class FreeTierEnforcementController(
    IFreeTierEnforcementOverviewService overviewService,
    IFreeTierUnsubscribedVpnUsersDailyDigestService unsubscribedVpnDigestService) : BaseController
{
    /// <summary>
    /// Every non-compliant Free/Default user (not channel-subscribed) — i.e. everyone the
    /// enforcement job would disconnect on its next run (subject to grace). Refresh manually; this
    /// evaluates Telegram channel membership per candidate and should not be polled automatically.
    /// </summary>
    [HttpGet("candidates")]
    public async Task<ActionResult<ApiResponse<GetFreeTierEnforcementCandidatesResponse>>> GetCandidates(
        CancellationToken ct)
    {
        var result = await overviewService.GetCandidatesAsync(ct);
        return Ok(ApiResponse<GetFreeTierEnforcementCandidatesResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// On-demand digest text of Free/Default users currently online without channel subscription
    /// (same payload as the daily admin Telegram digest).
    /// </summary>
    [HttpGet("unsubscribed-vpn-digest")]
    public async Task<ActionResult<ApiResponse<string>>> GetUnsubscribedVpnDigest(CancellationToken ct)
    {
        var text = await unsubscribedVpnDigestService.BuildDigestTextAsync(ct);
        return Ok(ApiResponse<string>.SuccessResponse(text));
    }

    [HttpGet("disconnect-log")]
    public async Task<ActionResult<ApiResponse<GetFreeTierDisconnectLogResponse>>> GetDisconnectLog(
        [FromQuery] GetFreeTierDisconnectLogRequest request, CancellationToken ct)
    {
        var result = await overviewService.GetDisconnectLogAsync(request, ct);
        return Ok(ApiResponse<GetFreeTierDisconnectLogResponse>.SuccessResponse(result));
    }
}
