using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

/// <summary>
/// Per-user grants and blocks for individual VPN servers, evaluated on top of the quota-plan allowlist.
/// </summary>
[ApiController]
[Route("api/user-vpn-server-access-rules")]
[Authorize]
public class UserVpnServerAccessRuleController(IUserVpnServerAccessRuleService service) : BaseController
{
    /// <summary>Get paged list. Optional filter by userId and/or vpnServerId (query).</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<GetAllUserVpnServerAccessRulesResponse>>> GetAll(
        [FromQuery] GetAllUserVpnServerAccessRulesRequest request,
        CancellationToken ct)
    {
        var response = await service.GetPageAsync(request, ct);
        return Ok(ApiResponse<GetAllUserVpnServerAccessRulesResponse>.SuccessResponse(response));
    }

    /// <summary>Get by id.</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpGet("get/{id:int}")]
    public async Task<ActionResult<ApiResponse<UserVpnServerAccessRuleResponse>>> GetById(int id, CancellationToken ct)
    {
        var response = await service.GetByIdAsync(id, ct);
        if (response is null)
            return NotFound(ApiResponse<UserVpnServerAccessRuleResponse>.ErrorResponse(
                "User VPN server access rule not found."));

        return Ok(ApiResponse<UserVpnServerAccessRuleResponse>.SuccessResponse(response));
    }

    /// <summary>Get all rules for a user. VpnUser may only query their own rules.</summary>
    [Authorize(Roles = "Admin,App,VpnUser")]
    [HttpGet("get-by-user-id/{userId:int}")]
    public async Task<ActionResult<ApiResponse<GetUserVpnServerAccessRulesByUserIdResponse>>> GetByUserId(
        int userId,
        CancellationToken ct)
    {
        if (!HttpUserContext.IsPrivileged(User))
        {
            if (!HttpUserContext.TryGetUserId(User, out var callerId))
                return Unauthorized(ApiResponse<GetUserVpnServerAccessRulesByUserIdResponse>.ErrorResponse(
                    "User id missing from token."));
            if (callerId != userId)
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<GetUserVpnServerAccessRulesByUserIdResponse>.ErrorResponse(
                        "You can only load your own VPN server access rules."));
        }

        var items = await service.GetListByUserIdAsync(userId, ct);
        return Ok(ApiResponse<GetUserVpnServerAccessRulesByUserIdResponse>.SuccessResponse(
            new GetUserVpnServerAccessRulesByUserIdResponse { Items = items }));
    }

    /// <summary>Get all users with a personal rule for a VPN server.</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpGet("get-by-vpn-server-id/{vpnServerId:int}")]
    public async Task<ActionResult<ApiResponse<GetUserVpnServerAccessRulesByVpnServerIdResponse>>> GetByVpnServerId(
        int vpnServerId,
        CancellationToken ct)
    {
        var items = await service.GetListByVpnServerIdAsync(vpnServerId, ct);
        return Ok(ApiResponse<GetUserVpnServerAccessRulesByVpnServerIdResponse>.SuccessResponse(
            new GetUserVpnServerAccessRulesByVpnServerIdResponse { Items = items }));
    }

    /// <summary>Grant or block a server for a user. Re-posting the same pair flips the mode.</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<UserVpnServerAccessRuleResponse>>> Create(
        [FromBody] CreateOrUpdateUserVpnServerAccessRuleRequest request,
        CancellationToken ct)
    {
        var response = await service.CreateAsync(request, ct);
        return Ok(ApiResponse<UserVpnServerAccessRuleResponse>.SuccessResponse(response));
    }

    /// <summary>Update an existing rule.</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpPut("update")]
    public async Task<ActionResult<ApiResponse<string>>> Update(
        [FromBody] CreateOrUpdateUserVpnServerAccessRuleRequest request,
        CancellationToken ct)
    {
        await service.UpdateAsync(request, ct);
        return Ok(ApiResponse<string>.SuccessResponse("Updated successfully"));
    }

    /// <summary>Delete a rule by id, restoring plain quota-plan behaviour for that pair.</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpDelete("delete/{id:int}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<string>.SuccessResponse("Deleted successfully"));
    }
}
