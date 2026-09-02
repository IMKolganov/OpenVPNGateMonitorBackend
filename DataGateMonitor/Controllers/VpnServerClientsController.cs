using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.DataBase.Services.Query.VpnServerClientTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Auth.Users;
using DataGateMonitor.Services.Api.Privacy;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerClients.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;
using System.Security.Claims;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/open-vpn-clients")]
[Route("api/v2/vpn-sessions")]
[Authorize]
public class VpnServerClientsController(
    IVpnServerClientOverviewQuery openVpnServerClientOverviewQuery,
    IVpnServerClientQueryService vpnServerClientQueryService,
    IOpenVpnGeoQueryService openVpnGeoQueryService,
    IOpenVpnOverviewTotalsQuery openVpnOverviewTotalsQuery,
    IOpenVpnOverviewSeriesQuery openVpnOverviewSeriesQuery,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IVpnServerAccessQueryService vpnServerAccessQueryService,
    IVpnServerQueryService vpnServerQueryService,
    IIssuedOvpnFileQueryService issuedOvpnFileQueryService,
    IUserQueryService userQueryService,
    IOpenVpnDisconnectExecutor openVpnDisconnectExecutor) : BaseController
{
    /// <summary>
    /// Admin-triggered disconnect of a connected OpenVPN client. Optionally revokes the client's
    /// certificate/ovpn file so it cannot silently reconnect with the same profile.
    /// </summary>
    [HttpPost("kill")]
    [Authorize(Roles = "Admin,App")]
    public async Task<ActionResult<ApiResponse<KillOpenVpnClientResponse>>> KillConnectedClient(
        [FromBody] KillOpenVpnClientRequest request, CancellationToken ct)
    {
        if (await VpnServerAuthorizationHelper.RequireVpnServerAccessOrForbidAsync<KillOpenVpnClientResponse>(
                User, vpnServerAccessQueryService, request.VpnServerId, ct) is { } deny)
            return deny;

        var server = await vpnServerQueryService.GetById(request.VpnServerId, ct);
        if (server is null)
            return NotFound(ApiResponse<KillOpenVpnClientResponse>.ErrorResponse(
                $"VPN server {request.VpnServerId} not found."));

        var externalId = await issuedOvpnFileQueryService.GetExternalIdByCommonName(
            request.CommonName, request.VpnServerId, ct);
        var user = !string.IsNullOrWhiteSpace(externalId)
            ? await userQueryService.GetByExternalId(externalId, ct)
            : null;

        var initiatedByUserId = HttpUserContext.TryGetUserId(User, out var adminUserId) ? adminUserId : (int?)null;

        var result = await openVpnDisconnectExecutor.ExecuteAsync(
            new OpenVpnDisconnectRequest
            {
                Server = server,
                Client = new Models.VpnServerClient
                {
                    CommonName = request.CommonName,
                    ManagementClientId = request.ManagementClientId,
                },
                UserId = user?.Id,
                UserDisplayNameSnapshot = user?.DisplayName ?? user?.Email,
                Reason = DisconnectReason.Manual,
                InitiatedByUserId = initiatedByUserId,
                RevokeCertificate = request.RevokeCertificate,
            },
            ct);

        return Ok(ApiResponse<KillOpenVpnClientResponse>.SuccessResponse(result));
    }


    [HttpGet("get-all-connected")]
    public async Task<ActionResult<ApiResponse<ConnectedClientsResponse>>> GetAllConnectedClients(
        [FromQuery] GetConnectedClientsRequest request, CancellationToken cancellationToken)
    {
        var result =
            await openVpnServerClientOverviewQuery.GetAllConnectedVpnServerClientsAsync(
            request, cancellationToken);

        var response = result.Adapt<ConnectedClientsResponse>();
        var ownExternalId = GetCurrentUserExternalId();
        var ownDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("displayName");
        var ownRows = CaptureOwnConnectedRowIndexes(response, ownExternalId);
        ClientStatisticsResponseSanitizer.ApplyIfNeeded(User, response);
        RestoreOwnConnectedRowIdentity(response, ownRows, ownExternalId, ownDisplayName);
        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// VPN server ids where the current user has at least one connected session.
    /// Lightweight alternative to calling get-all-connected per server from the UI.
    /// </summary>
    [HttpGet("user-connected-server-ids")]
    public async Task<ActionResult<ApiResponse<UserConnectedServerIdsResponse>>> GetUserConnectedServerIds(
        CancellationToken ct)
    {
        var externalId = await ResolveEffectiveExternalIdAsync(null, ct);
        var userId = HttpUserContext.TryGetUserId(User, out var resolvedUserId) ? resolvedUserId : (int?)null;
        var serverIds = await vpnServerClientQueryService.GetConnectedVpnServerIdsForUserAsync(userId, externalId, ct);

        return Ok(ApiResponse<UserConnectedServerIdsResponse>.SuccessResponse(new UserConnectedServerIdsResponse
        {
            VpnServerIds = serverIds.ToList(),
        }));
    }

    [HttpGet("get-all-history")]
    public async Task<ActionResult<ApiResponse<ConnectedClientsResponse>>> GetAllHistoryClients(
        [FromQuery] GetHistoryClientsRequest request, CancellationToken ct)
    {
        if (await VpnServerAuthorizationHelper.RequireVpnServerAccessOrForbidAsync<ConnectedClientsResponse>(
                User, vpnServerAccessQueryService, request.VpnServerId, ct) is { } deny)
            return deny;

        var result = 
            await openVpnServerClientOverviewQuery.GetAllHistoryVpnServerClientsAsync(
            request, ct);

        var response = result.Adapt<ConnectedClientsResponse>();
        var ownExternalId = GetCurrentUserExternalId();
        var ownDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("displayName");
        var ownRows = CaptureOwnConnectedRowIndexes(response, ownExternalId);
        ClientStatisticsResponseSanitizer.ApplyIfNeeded(User, response);
        RestoreOwnConnectedRowIdentity(response, ownRows, ownExternalId, ownDisplayName);
        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(response));
    }
    
    [HttpGet("overview/series")]
    public async Task<ActionResult<ApiResponse<OverviewSeriesResponse>>> GetOverview(
        [FromQuery] GetOverviewSeriesRequest request,
        CancellationToken ct = default)
    {
        var externalId = await ResolveEffectiveExternalIdAsync(request.ExternalId, ct);

        var result = await openVpnOverviewSeriesQuery.GetOverviewSeriesFromSessionsAsync(
            request.From,
            request.To,
            request.Grouping,
            request.VpnServerId,
            externalId,
            ct);

        return Ok(ApiResponse<OverviewSeriesResponse>.SuccessResponse(result));
    }

    [HttpGet("overview/summary")]
    public async Task<ActionResult<ApiResponse<OverviewTotalsResponse>>> GetOverviewSummary(
        [FromQuery] GetOverviewSummaryRequest request,
        CancellationToken ct = default)
    {
        var externalId = await ResolveEffectiveExternalIdAsync(request.ExternalId, ct);

        var result = await openVpnOverviewTotalsQuery.GetOverviewTotalsAsync(
            request.From,
            request.To,
            request.VpnServerId,
            externalId,
            ct);
    
        return Ok(ApiResponse<OverviewTotalsResponse>.SuccessResponse(result));
    }
    
    [HttpGet("overview/points")]
    public async Task<ActionResult<ApiResponse<OverviewPointsResponse>>> GetPoints(
        [FromQuery] GetOverviewPointsRequest request,
        CancellationToken ct = default)
    {
        var externalId = await ResolveEffectiveExternalIdAsync(request.ExternalId, ct);

        var points = await openVpnGeoQueryService.GetGeoPointsAsync(
            request.From,
            request.To,
            request.VpnServerId,
            externalId,
            request.OnlyWithCoordinates,
            ct);

        return Ok(ApiResponse<OverviewPointsResponse>.SuccessResponse(points));
    }
    
    [HttpGet("overview/users")]
    public async Task<ActionResult<ApiResponse<OverviewUsersResponse>>> GetOverviewUsers(
        [FromQuery] GetOverviewUsersRequest request,
        CancellationToken ct = default)
    {
        var externalId = NormalizeExternalId(request.ExternalId);

        var users = await openVpnOverviewSeriesQuery.GetOverviewUsersFromSessionsAsync(
            request.From,
            request.To,
            request.VpnServerId,
            externalId,
            request.DisplayName,
            ct);

        var ownExternalId = GetCurrentUserExternalId();
        var ownDisplayName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("displayName");
        var ownRows = CaptureOwnOverviewUsersRowIndexes(users, ownExternalId);
        ClientStatisticsResponseSanitizer.ApplyIfNeeded(User, users);
        RestoreOwnOverviewUsersRowIdentity(users, ownRows, ownExternalId, ownDisplayName);
        return Ok(ApiResponse<OverviewUsersResponse>.SuccessResponse(users));
    }

    /// <summary>
    /// Time-bucketed series of session count and unique user count per bucket. Same query params as overview/series (From, To, Grouping, VpnServerId, ExternalId).
    /// </summary>
    [HttpGet("overview/users/series")]
    public async Task<ActionResult<ApiResponse<OverviewUsersSeriesResponse>>> GetOverviewUsersSeries(
        [FromQuery] GetOverviewSeriesRequest request,
        CancellationToken ct = default)
    {
        var externalId = NormalizeExternalId(request.ExternalId);

        var result = await openVpnOverviewSeriesQuery.GetOverviewUsersSeriesFromSessionsAsync(
            request.From,
            request.To,
            request.Grouping,
            request.VpnServerId,
            externalId,
            ct);

        return Ok(ApiResponse<OverviewUsersSeriesResponse>.SuccessResponse(result));
    }

    private static string? NormalizeExternalId(string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        var trimmed = externalId.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private string? GetCurrentUserExternalId() => NormalizeExternalId(
        User.FindFirstValue("externalId") ??
        User.FindFirstValue("ExternalId"));

    private static List<int> CaptureOwnConnectedRowIndexes(
        ConnectedClientsResponse response,
        string? ownExternalId)
    {
        if (string.IsNullOrWhiteSpace(ownExternalId))
            return [];

        var captured = new List<int>();
        for (var i = 0; i < response.VpnClients.Count; i++)
        {
            var row = response.VpnClients[i];
            if (!string.Equals(NormalizeExternalId(row.ExternalId), ownExternalId, StringComparison.Ordinal))
                continue;

            captured.Add(i);
        }

        return captured;
    }

    private static void RestoreOwnConnectedRowIdentity(
        ConnectedClientsResponse response,
        List<int> ownRows,
        string? ownExternalId,
        string? ownDisplayName)
    {
        foreach (var index in ownRows)
        {
            if (index >= 0 && index < response.VpnClients.Count)
            {
                if (!string.IsNullOrWhiteSpace(ownExternalId))
                    response.VpnClients[index].ExternalId = ownExternalId;
                if (!string.IsNullOrWhiteSpace(ownDisplayName))
                    response.VpnClients[index].DisplayName = ownDisplayName;
            }
        }
    }

    private static List<int> CaptureOwnOverviewUsersRowIndexes(
        OverviewUsersResponse response,
        string? ownExternalId)
    {
        if (string.IsNullOrWhiteSpace(ownExternalId))
            return [];

        var captured = new List<int>();
        for (var i = 0; i < response.OverviewUserItems.Count; i++)
        {
            var row = response.OverviewUserItems[i];
            if (!string.Equals(NormalizeExternalId(row.ExternalId), ownExternalId, StringComparison.Ordinal))
                continue;

            captured.Add(i);
        }

        return captured;
    }

    private static void RestoreOwnOverviewUsersRowIdentity(
        OverviewUsersResponse response,
        List<int> ownRows,
        string? ownExternalId,
        string? ownDisplayName)
    {
        foreach (var index in ownRows)
        {
            if (index >= 0 && index < response.OverviewUserItems.Count)
            {
                response.OverviewUserItems[index].ExternalId = ownExternalId;
                if (!string.IsNullOrWhiteSpace(ownDisplayName))
                    response.OverviewUserItems[index].DisplayName = ownDisplayName;
            }
        }
    }

    private async Task<string?> ResolveEffectiveExternalIdAsync(string? externalId, CancellationToken ct)
    {
        var normalized = NormalizeExternalId(externalId);
        var isAdmin = HttpUserContext.IsPrivileged(User);

        // Admin keeps explicit filtering behavior; omitted ExternalId means global analytics.
        if (isAdmin)
            return normalized;

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                          ?? User.FindFirstValue("nameid")
                          ?? User.FindFirstValue("sub");
        if (int.TryParse(userIdClaim, out var userId))
        {
            // For regular users, trust server-side identity link as the source of truth.
            var linkedExternalId = NormalizeExternalId(
                await UserIdentityLinkExternalIdResolver.ResolveAsync(userId, userIdentityLinkQueryService, ct));
            if (!string.IsNullOrWhiteSpace(linkedExternalId))
                return linkedExternalId;
        }

        // Fallback for older tokens when identity link is missing/inconsistent.
        var claimExternalId = NormalizeExternalId(
            User.FindFirstValue("externalId") ??
            User.FindFirstValue("ExternalId"));
        if (!string.IsNullOrWhiteSpace(claimExternalId))
            return claimExternalId;

        // Last resort (legacy clients) - still better than dropping all data.
        return normalized;
    }
}
