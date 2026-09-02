using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.VpnManagerReleases;
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
    IUserVpnServerAccessRuleQueryService userVpnServerAccessRuleQueryService,
    IApiMemoryCacheService apiMemoryCacheService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IConnectedClientsCounterStore connectedClientsCounterStore,
    IVpnManagerUpdateStatusEnricher vpnManagerUpdateStatusEnricher) : BaseController
{
    private static readonly TimeSpan ServersListCacheTtl = TimeSpan.FromHours(1);

    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServersV2Response>>> GetAllServers(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var access = await ResolveAccessibleServerIdsAsync(ct);
        if (!access.HasUserContext)
            return Unauthorized(ApiResponse<VpnServersV2Response>.ErrorResponse("User id missing from token."));

        var cacheKey = $"v2:open-vpn-servers:get-all:includeDeleted={includeDeleted}:scope={access.CacheScopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            access.RestrictToQuotaPlanId,
            access.Overrides,
            ct);
        var stampKey = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";

        async Task<ApiResponse<VpnServersV2Response>> BuildResponse(CancellationToken token)
        {
            var serversList = await openVpnServerQueryService.GetAll(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                access.RestrictToQuotaPlanId,
                access.Overrides,
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
                v2.IsAccessibleForUserQuotaPlan = access.IsAccessible(server.Id);
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
        var access = await ResolveAccessibleServerIdsAsync(ct);
        if (!access.HasUserContext)
            return Unauthorized(ApiResponse<VpnServerWithStatusesV2Response>.ErrorResponse("User id missing from token."));

        var cacheKey = $"v2:open-vpn-servers:get-all-with-status:includeDeleted={includeDeleted}:scope={access.CacheScopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            access.RestrictToQuotaPlanId,
            access.Overrides,
            ct);
        var dataStamp = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";
        var stampKey = $"{dataStamp}:status:{statusCacheGenerationService.CurrentStamp}";
        async Task<ApiResponse<VpnServerWithStatusesV2Response>> BuildResponse(CancellationToken token)
        {
            var result = await openVpnServerOverviewQuery.GetAllVpnServersWithStatusAsync(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                access.RestrictToQuotaPlanId,
                access.Overrides,
                token);
            await VpnServerConnectedCountOverlay.ApplyAsync(result, connectedClientsCounterStore, token);
            await vpnManagerUpdateStatusEnricher.EnrichAsync(result, token);
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
                    TotalBytesOut = item.TotalBytesOut,
                    InstalledManagerVersion = item.InstalledManagerVersion,
                    LatestManagerVersion = item.LatestManagerVersion,
                    IsManagerUpdateAvailable = item.IsManagerUpdateAvailable,
                    ManagerReleaseUrl = item.ManagerReleaseUrl
                };
                v2.VpnServerResponses.VpnServer.QuotaPlanGroups = groups.GetValueOrDefault(id, []);
                v2.VpnServerResponses.VpnServer.IsAccessibleForUserQuotaPlan = access.IsAccessible(id);
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

    /// <returns>Privileged: unrestricted. Non-privileged with a plan: the plan ids plus the user's personal rules. Missing user id: <c>HasUserContext</c> false.</returns>
    private async Task<ServerAccess> ResolveAccessibleServerIdsAsync(CancellationToken ct)
    {
        if (HttpUserContext.IsPrivileged(User))
            return new ServerAccess(null, null, UserVpnServerAccessOverrides.None, true);
        if (!HttpUserContext.TryGetUserId(User, out var userId))
            return new ServerAccess(null, null, UserVpnServerAccessOverrides.None, false);

        var overrides = await userVpnServerAccessRuleQueryService.GetOverridesByUserId(userId, ct);
        var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        if (uqp is null)
            return new ServerAccess(null, null, overrides, true);

        var set = await quotaPlanAllowedServerQueryService.GetVpnServerIdsByQuotaPlanId(uqp.QuotaPlanId, ct);
        return new ServerAccess(set, uqp.QuotaPlanId, overrides, true);
    }

    private sealed record ServerAccess(
        HashSet<int>? PlanAllowedServerIds,
        int? RestrictToQuotaPlanId,
        UserVpnServerAccessOverrides Overrides,
        bool HasUserContext)
    {
        public string CacheScopeKey =>
            (RestrictToQuotaPlanId is int planId ? $"plan:{planId}" : "all") + Overrides.CacheScopeSuffix;

        public bool IsAccessible(int vpnServerId) =>
            Overrides.Allows(vpnServerId, PlanAllowedServerIds is null || PlanAllowedServerIds.Contains(vpnServerId));
    }
}

