using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

public static class VpnServerAuthorizationHelper
{
    /// <typeparam name="T">Success payload type for <see cref="ApiResponse{T}"/> on this action (error bodies may still use <see cref="string"/>).</typeparam>
    public static async Task<ActionResult<ApiResponse<T>>?> RequireVpnServerAccessOrForbidAsync<T>(
        ClaimsPrincipal user,
        IVpnServerAccessQueryService access,
        int vpnServerId,
        CancellationToken ct)
    {
        if (HttpUserContext.IsPrivileged(user))
            return null;
        if (!HttpUserContext.TryGetUserId(user, out var userId))
            return new ActionResult<ApiResponse<T>>(new UnauthorizedObjectResult(
                ApiResponse<string>.ErrorResponse("User id missing from token.")));
        if (!await access.UserHasAccessAsync(userId, vpnServerId, ct))
            return new ActionResult<ApiResponse<T>>(new ObjectResult(ApiResponse<string>.ErrorResponse(
                    VpnServerAccessErrorKeys.NotAllowedByQuotaPlan))
                { StatusCode = StatusCodes.Status403Forbidden });
        return null;
    }
}
