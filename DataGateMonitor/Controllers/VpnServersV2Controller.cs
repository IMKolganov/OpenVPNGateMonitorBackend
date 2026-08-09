using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

/// <summary>
/// API v2: all servers (non-deleted) with quota-plan groups and <see cref="VpnServerV2Dto.IsAccessibleForUserQuotaPlan"/>.
/// Admin/App see every server with accessibility true; other users see all servers but only their plan's servers are marked accessible.
/// </summary>
[ApiController]
[Route("api/v2/open-vpn-servers")]
[Authorize]
public class VpnServersV2Controller(
    IVpnServerOverviewQuery openVpnServerOverviewQuery,
    IVpnServerQueryService openVpnServerQueryService,
    IVpnServerQuotaPlanGroupsQuery quotaPlanGroupsQuery,
    IVpnServerTagQueryService openVpnServerTagQueryService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService,
    IApiMemoryCacheService apiMemoryCacheService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IConnectedClientsCounterStore connectedClientsCounterStore) : BaseController
{
    private static readonly TimeSpan ServersListCacheTtl = TimeSpan.FromHours(1);

    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServersV2Response>>> GetAllServers(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var (allowedSet, restrictToQuotaPlanId, hasUserContext) = await ResolveAccessibleServerIdsAsync(ct);
        if (!hasUserContext)
            return Unauthorized(ApiResponse<VpnServersV2Response>.ErrorResponse("User id missing from token."));

        var scopeKey = restrictToQuotaPlanId is int quotaPlanId ? $"plan:{quotaPlanId}" : "all";
        var cacheKey = $"v2:open-vpn-servers:get-all:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId,
            ct);
        var stampKey = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";

        async Task<ApiResponse<VpnServersV2Response>> BuildResponse(CancellationToken token)
        {
            var serversList = await openVpnServerQueryService.GetAll(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                restrictToQuotaPlanId,
                token);
            var response = new VpnServersV2Response();
            if (serversList.Count == 0)
                return ApiResponse<VpnServersV2Response>.SuccessResponse(response);

            var ids = serversList.Select(s => s.Id).ToList();
            var groups = await quotaPlanGroupsQuery.GetGroupsByVpnServerIdsAsync(ids, token);
            var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(ids, token);

            foreach (var server in serversList)
            {
                var dto = server.Adapt<VpnServerDto>();
                dto.Tags = tagNamesByServer.GetValueOrDefault(server.Id, []);
                var v2 = dto.Adapt<VpnServerV2Dto>();
                v2.QuotaPlanGroups = groups.GetValueOrDefault(server.Id, []);
                v2.IsAccessibleForUserQuotaPlan = allowedSet is null || allowedSet.Contains(server.Id);
                response.VpnServers.Add(v2);
            }

            return ApiResponse<VpnServersV2Response>.SuccessResponse(response);
        }

        ApiResponse<VpnServersV2Response> cached;
        if (withoutCache)
        {
            cached = await BuildResponse(ct);
            apiMemoryCacheService.Set(cacheKey, cached, ServersListCacheTtl, stampKey);
        }
        else
        {
            cached = await apiMemoryCacheService.GetOrCreateByStampAsync(
                cacheKey,
                stampKey,
                BuildResponse,
                ServersListCacheTtl,
                ct);
        }

        return Ok(cached);
    }

    [HttpGet("get-all-with-status")]
    public async Task<ActionResult<ApiResponse<VpnServerWithStatusesV2Response>>> GetAllServersWithStatus(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var (allowedSet, restrictToQuotaPlanId, hasUserContext) = await ResolveAccessibleServerIdsAsync(ct);
        if (!hasUserContext)
            return Unauthorized(ApiResponse<VpnServerWithStatusesV2Response>.ErrorResponse("User id missing from token."));

        var scopeKey = restrictToQuotaPlanId is int quotaPlanId ? $"plan:{quotaPlanId}" : "all";
        var cacheKey = $"v2:open-vpn-servers:get-all-with-status:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId,
            ct);
        var dataStamp = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";
        var stampKey = $"{dataStamp}:status:{statusCacheGenerationService.CurrentStamp}";
        async Task<ApiResponse<VpnServerWithStatusesV2Response>> BuildResponse(CancellationToken token)
        {
            var result = await openVpnServerOverviewQuery.GetAllVpnServersWithStatusAsync(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                restrictToQuotaPlanId,
                token);
            await VpnServerConnectedCountOverlay.ApplyAsync(result, connectedClientsCounterStore, token);
            var response = new VpnServerWithStatusesV2Response();
            if (result.Count == 0)
                return ApiResponse<VpnServerWithStatusesV2Response>.SuccessResponse(response);

            var ids = result.Select(x => x.VpnServerResponses.VpnServer.Id).ToList();
            var groups = await quotaPlanGroupsQuery.GetGroupsByVpnServerIdsAsync(ids, token);
            var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(ids, token);

            foreach (var item in result)
            {
                var id = item.VpnServerResponses.VpnServer.Id;
                item.VpnServerResponses.VpnServer.Tags = tagNamesByServer.GetValueOrDefault(id, []);
                var v2 = new VpnServerWithStatusV2Dto
                {
                    VpnServerResponses = new VpnServerV2Response
                    {
                        VpnServer = item.VpnServerResponses.VpnServer.Adapt<VpnServerV2Dto>()
                    },
                    VpnServerStatusLogResponse = item.VpnServerStatusLogResponse,
                    CountConnectedClients = item.CountConnectedClients,
                    CountSessions = item.CountSessions,
                    TotalBytesIn = item.TotalBytesIn,
                    TotalBytesOut = item.TotalBytesOut
                };
                v2.VpnServerResponses.VpnServer.QuotaPlanGroups = groups.GetValueOrDefault(id, []);
                v2.VpnServerResponses.VpnServer.IsAccessibleForUserQuotaPlan =
                    allowedSet is null || allowedSet.Contains(id);
                response.VpnServerWithStatuses.Add(v2);
            }

            return ApiResponse<VpnServerWithStatusesV2Response>.SuccessResponse(response);
        }

        ApiResponse<VpnServerWithStatusesV2Response> cached;
        if (withoutCache)
        {
            cached = await BuildResponse(ct);
            apiMemoryCacheService.Set(cacheKey, cached, ServersListCacheTtl, stampKey);
        }
        else
        {
            cached = await apiMemoryCacheService.GetOrCreateByStampAsync(
                cacheKey,
                stampKey,
                BuildResponse,
                ServersListCacheTtl,
                ct);
        }

        if (cached.Data?.VpnServerWithStatuses is { Count: > 0 } statuses)
            await VpnServerConnectedCountOverlay.ApplyAsync(statuses, connectedClientsCounterStore, ct);

        return Ok(cached);
    }

    /// <returns>Privileged: (null, null, true). Non-privileged with plan: (allowed ids, planId, true). Missing user id: (_, _, false).</returns>
    private async Task<(HashSet<int>? AllowedServerIds, int? RestrictToQuotaPlanId, bool HasUserContext)> ResolveAccessibleServerIdsAsync(
        CancellationToken ct)
    {
        if (HttpUserContext.IsPrivileged(User))
            return (null, null, true);
        if (!HttpUserContext.TryGetUserId(User, out var userId))
            return (null, null, false);
        var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        if (uqp is null)
            return (null, null, true);
        var set = await quotaPlanAllowedServerQueryService.GetVpnServerIdsByQuotaPlanId(uqp.QuotaPlanId, ct);
        return (set, uqp.QuotaPlanId, true);
    }
}

