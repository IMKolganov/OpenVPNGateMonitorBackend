using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerGroupTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

/// <summary>
/// API v3: returns every VPN server (no quota-plan list filter) with per-server quota metadata
/// (<see cref="VpnServerV2Dto.QuotaPlanGroups"/>, <see cref="VpnServerV2Dto.IsAccessibleForUserQuotaPlan"/>)
/// and user quota context for the caller. Keeps v1/v2 list filtering for legacy mobile clients.
/// </summary>
[ApiController]
[Route("api/v3/open-vpn-servers")]
[Authorize]
public class VpnServersV3Controller(
    IVpnServerOverviewQuery openVpnServerOverviewQuery,
    IVpnServerQueryService openVpnServerQueryService,
    IVpnServerQuotaPlanGroupsQuery quotaPlanGroupsQuery,
    IVpnServerTagQueryService openVpnServerTagQueryService,
    IVpnServerGroupQueryService vpnServerGroupQueryService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IQuotaPlanAllowedServerQueryService quotaPlanAllowedServerQueryService,
    IQuotaPlanQueryService quotaPlanQueryService,
    IApiMemoryCacheService apiMemoryCacheService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IConnectedClientsCounterStore connectedClientsCounterStore) : BaseController
{
    private static readonly TimeSpan ServersListCacheTtl = TimeSpan.FromHours(1);

    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServersV3Response>>> GetAllServers(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var access = await ResolveQuotaAccessAsync(ct);
        if (!access.HasUserContext)
            return Unauthorized(ApiResponse<VpnServersV3Response>.ErrorResponse("User id missing from token."));

        var scopeKey = access.CacheScopeKey;
        var cacheKey = $"v3:open-vpn-servers:get-all:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null,
            ct);
        var stampKey = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";

        async Task<ApiResponse<VpnServersV3Response>> BuildResponse(CancellationToken token)
        {
            var serversList = await openVpnServerQueryService.GetAll(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                restrictToQuotaPlanId: null,
                token);
            var response = new VpnServersV3Response { UserQuotaPlan = access.Context };
            if (serversList.Count == 0)
                return ApiResponse<VpnServersV3Response>.SuccessResponse(response);

            var ids = serversList.Select(s => s.Id).ToList();
            var groups = await quotaPlanGroupsQuery.GetGroupsByVpnServerIdsAsync(ids, token);
            var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(ids, token);
            var groupNames = await LoadGroupNamesAsync(token);

            foreach (var server in serversList)
            {
                var dto = server.Adapt<VpnServerDto>();
                dto.Tags = tagNamesByServer.GetValueOrDefault(server.Id, []);
                dto.GroupId = server.VpnServerGroupId;
                dto.SortOrder = server.SortOrder;
                dto.GroupName = server.VpnServerGroupId is int gid
                    ? groupNames.GetValueOrDefault(gid)
                    : null;
                var v2 = dto.Adapt<VpnServerV2Dto>();
                v2.QuotaPlanGroups = groups.GetValueOrDefault(server.Id, []);
                v2.IsAccessibleForUserQuotaPlan =
                    access.AllowedServerIds is null || access.AllowedServerIds.Contains(server.Id);
                response.VpnServers.Add(v2);
            }

            return ApiResponse<VpnServersV3Response>.SuccessResponse(response);
        }

        return Ok(await GetOrCreateCachedAsync(cacheKey, stampKey, BuildResponse, withoutCache, ct));
    }

    [HttpGet("get-all-with-status")]
    public async Task<ActionResult<ApiResponse<VpnServerWithStatusesV3Response>>> GetAllServersWithStatus(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var access = await ResolveQuotaAccessAsync(ct);
        if (!access.HasUserContext)
            return Unauthorized(ApiResponse<VpnServerWithStatusesV3Response>.ErrorResponse("User id missing from token."));

        var scopeKey = access.CacheScopeKey;
        var cacheKey = $"v3:open-vpn-servers:get-all-with-status:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId: null,
            ct);
        var dataStamp = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";
        var stampKey = $"{dataStamp}:status:{statusCacheGenerationService.CurrentStamp}";

        async Task<ApiResponse<VpnServerWithStatusesV3Response>> BuildResponse(CancellationToken token)
        {
            var result = await openVpnServerOverviewQuery.GetAllVpnServersWithStatusAsync(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                restrictToQuotaPlanId: null,
                token);
            await VpnServerConnectedCountOverlay.ApplyAsync(result, connectedClientsCounterStore, token);
            var response = new VpnServerWithStatusesV3Response { UserQuotaPlan = access.Context };
            if (result.Count == 0)
                return ApiResponse<VpnServerWithStatusesV3Response>.SuccessResponse(response);

            var ids = result.Select(x => x.VpnServerResponses.VpnServer.Id).ToList();
            var groups = await quotaPlanGroupsQuery.GetGroupsByVpnServerIdsAsync(ids, token);
            var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(ids, token);
            var groupNames = await LoadGroupNamesAsync(token);

            foreach (var item in result)
            {
                var id = item.VpnServerResponses.VpnServer.Id;
                var srcDto = item.VpnServerResponses.VpnServer;
                srcDto.Tags = tagNamesByServer.GetValueOrDefault(id, []);
                srcDto.GroupName = srcDto.GroupId is int gid
                    ? groupNames.GetValueOrDefault(gid)
                    : null;
                var v2 = new VpnServerWithStatusV2Dto
                {
                    VpnServerResponses = new VpnServerV2Response
                    {
                        VpnServer = srcDto.Adapt<VpnServerV2Dto>()
                    },
                    VpnServerStatusLogResponse = item.VpnServerStatusLogResponse,
                    CountConnectedClients = item.CountConnectedClients,
                    CountSessions = item.CountSessions,
                    TotalBytesIn = item.TotalBytesIn,
                    TotalBytesOut = item.TotalBytesOut
                };
                v2.VpnServerResponses.VpnServer.QuotaPlanGroups = groups.GetValueOrDefault(id, []);
                v2.VpnServerResponses.VpnServer.IsAccessibleForUserQuotaPlan =
                    access.AllowedServerIds is null || access.AllowedServerIds.Contains(id);
                response.VpnServerWithStatuses.Add(v2);
            }

            return ApiResponse<VpnServerWithStatusesV3Response>.SuccessResponse(response);
        }

        var cached = await GetOrCreateCachedAsync(cacheKey, stampKey, BuildResponse, withoutCache, ct);
        if (cached.Data?.VpnServerWithStatuses is { Count: > 0 } statuses)
            await VpnServerConnectedCountOverlay.ApplyAsync(statuses, connectedClientsCounterStore, ct);
        return Ok(cached);
    }

    private async Task<ApiResponse<T>> GetOrCreateCachedAsync<T>(
        string cacheKey,
        string stampKey,
        Func<CancellationToken, Task<ApiResponse<T>>> build,
        bool withoutCache,
        CancellationToken ct)
    {
        if (withoutCache)
        {
            var fresh = await build(ct);
            apiMemoryCacheService.Set(cacheKey, fresh, ServersListCacheTtl, stampKey);
            return fresh;
        }

        return await apiMemoryCacheService.GetOrCreateByStampAsync(
            cacheKey,
            stampKey,
            build,
            ServersListCacheTtl,
            ct);
    }

    private async Task<QuotaAccess> ResolveQuotaAccessAsync(CancellationToken ct)
    {
        if (HttpUserContext.IsPrivileged(User))
        {
            return new QuotaAccess(
                HasUserContext: true,
                AllowedServerIds: null,
                CacheScopeKey: "privileged",
                Context: new UserQuotaPlanContextDto { IsPrivileged = true });
        }

        if (!HttpUserContext.TryGetUserId(User, out var userId))
            return new QuotaAccess(false, null, "anonymous", new UserQuotaPlanContextDto());

        var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        if (uqp is null)
        {
            return new QuotaAccess(
                HasUserContext: true,
                AllowedServerIds: null,
                CacheScopeKey: "unrestricted",
                Context: new UserQuotaPlanContextDto());
        }

        var allowed = await quotaPlanAllowedServerQueryService.GetVpnServerIdsByQuotaPlanId(uqp.QuotaPlanId, ct);
        var plan = await quotaPlanQueryService.GetById(uqp.QuotaPlanId, ct);
        var context = new UserQuotaPlanContextDto
        {
            UserQuotaPlanId = uqp.Id,
            QuotaPlanId = uqp.QuotaPlanId,
            QuotaPlanName = plan?.Name,
            AllowedVpnServerIds = allowed.OrderBy(x => x).ToList()
        };
        return new QuotaAccess(
            HasUserContext: true,
            AllowedServerIds: allowed,
            CacheScopeKey: $"plan:{uqp.QuotaPlanId}",
            Context: context);
    }

    private async Task<Dictionary<int, string>> LoadGroupNamesAsync(CancellationToken ct)
    {
        var groups = await vpnServerGroupQueryService.GetAll(ct);
        return groups.ToDictionary(g => g.Id, g => g.Name);
    }

    private sealed record QuotaAccess(
        bool HasUserContext,
        HashSet<int>? AllowedServerIds,
        string CacheScopeKey,
        UserQuotaPlanContextDto Context);
}
