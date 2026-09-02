using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using PostSetupStatus = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupStatus;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.StatusStreamLogs;
using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;
using DataGateMonitor.SharedModels.Enums;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/open-vpn-servers")]
[Authorize]
public class VpnServersController(IVpnDataService vpnDataService,
    IVpnServerDiscoveryService vpnServerDiscoveryService,
    IVpnServerOverviewQuery openVpnServerOverviewQuery, IVpnServerQueryService openVpnServerQueryService,
    IVpnServerTagQueryService openVpnServerTagQueryService,
    IOpenVpnBackgroundService openVpnBackgroundService,
    IMicroserviceInfoService microserviceInfoService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IUserVpnServerAccessRuleQueryService userVpnServerAccessRuleQueryService,
    IVpnServerAccessQueryService vpnServerAccessQueryService,
    IApiMemoryCacheService apiMemoryCacheService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IStatusStreamLogStore statusStreamLogStore,
    IVpnServerPostSetupService vpnServerPostSetupService,
    IConnectedClientsCounterStore connectedClientsCounterStore,
    IVpnServerOvpnFileConfigQueryService ovpnFileConfigQueryService,
    IVpnManagerUpdateStatusEnricher vpnManagerUpdateStatusEnricher) : BaseController
{
    private static readonly TimeSpan ServersListCacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Legacy Android ≤1.0.4 list. Omits Xray and UDP OpenVPN nodes because those clients
    /// treat every WSS entry as TCP and reconnect against UDP-only listeners.
    /// </summary>
    [HttpGet("get-all-with-status")]
    public async Task<ActionResult> GetAllServersWithStatus(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var scope = await ResolveQuotaScopeAsync(ct);
        if (scope is null)
            return Unauthorized(ApiResponse<VpnServerWithStatusesResponse>.ErrorResponse("User id missing from token."));

        var cacheKey = $"v1:open-vpn-servers:get-all-with-status:legacyTcpOpenVpnOnly:ovpnProto:includeDeleted={includeDeleted}:scope={scope.CacheScopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            scope.RestrictToQuotaPlanId,
            scope.Overrides,
            ct);
        var dataStamp = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";
        var stampKey = $"{dataStamp}:status:{statusCacheGenerationService.CurrentStamp}";
        async Task<string> BuildPayload(CancellationToken token)
        {
            var result = await openVpnServerOverviewQuery.GetAllVpnServersWithStatusAsync(
                includeDeleted, requireQuotaPlanAssignment: false, scope.RestrictToQuotaPlanId, scope.Overrides, token);
            await VpnServerConnectedCountOverlay.ApplyAsync(result, connectedClientsCounterStore, token);
            await vpnManagerUpdateStatusEnricher.EnrichAsync(result, token);

            var baseResponse = new VpnServerWithStatusesResponse
            {
                VpnServerWithStatuses = result
            };
            await FillTagsForOverviewResponse(baseResponse, token);
            baseResponse.VpnServerWithStatuses = await FilterLegacyTcpOpenVpnOverviewAsync(
                baseResponse.VpnServerWithStatuses,
                token);

            // Legacy mobile clients parse strict camelCase + openVpn* keys.
            var envelope = new LegacyVpnServerWithStatusesEnvelope
            {
                Data = new LegacyVpnServerWithStatusesData
                {
                    OpenVpnServerWithStatuses = baseResponse.VpnServerWithStatuses.Select(item =>
                        new LegacyVpnServerWithStatusItem
                        {
                            OpenVpnServerResponses = new LegacyVpnServerResponses
                            {
                                OpenVpnServer = item.VpnServerResponses.VpnServer
                            },
                            OpenVpnServerStatusLogResponse = item.VpnServerStatusLogResponse,
                            CountConnectedClients = item.CountConnectedClients,
                            CountSessions = item.CountSessions,
                            TotalBytesIn = item.TotalBytesIn,
                            TotalBytesOut = item.TotalBytesOut
                        }).ToList()
                }
            };

            return JsonConvert.SerializeObject(
                envelope,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                });
        }

        string json;
        if (withoutCache)
        {
            json = await BuildPayload(ct);
            apiMemoryCacheService.Set(cacheKey, json, ServersListCacheTtl, stampKey);
        }
        else
        {
            json = await apiMemoryCacheService.GetOrCreateByStampAsync(cacheKey, stampKey, BuildPayload, ServersListCacheTtl, ct);
        }

        return Content(json, "application/json");
    }

    [HttpGet("get-server-with-status/{VpnServerId:int}")]
    public async Task<ActionResult<ApiResponse<VpnServerWithStatusResponse>>> GetServerWithStatus(
        [FromRoute] GetServerWithStatsRequest request, CancellationToken ct)
    {
        if (await VpnServerAuthorizationHelper.RequireVpnServerAccessOrForbidAsync<VpnServerWithStatusResponse>(User, vpnServerAccessQueryService,
                request.VpnServerId, ct) is { } denyStatus)
            return denyStatus;

        var serverInfo = await openVpnServerOverviewQuery.GetVpnServerWithStatusAsync(request.VpnServerId, ct);
        await vpnManagerUpdateStatusEnricher.EnrichAsync([serverInfo], ct);
        var response = new VpnServerWithStatusResponse { VpnServerWithStatus = serverInfo };
        if (response.VpnServerWithStatus?.VpnServerResponses?.VpnServer != null)
            response.VpnServerWithStatus.VpnServerResponses.VpnServer.Tags = await openVpnServerTagQueryService.GetTagNamesByVpnServerId(request.VpnServerId, ct);
        return Ok(ApiResponse<VpnServerWithStatusResponse>.SuccessResponse(response));
    }

    [HttpGet("get-microservice-info/{vpnServerId:int}")]
    public async Task<ActionResult<ApiResponse<VpnMicroserviceDiagnosticsDto>>> GetMicroserviceInfo(
        [FromRoute] int vpnServerId, CancellationToken ct)
    {
        if (await VpnServerAuthorizationHelper.RequireVpnServerAccessOrForbidAsync<VpnMicroserviceDiagnosticsDto>(User, vpnServerAccessQueryService,
                vpnServerId, ct) is { } denyMicro)
            return denyMicro;

        var info = await microserviceInfoService.GetInfoAsync(vpnServerId, ct);
        return Ok(ApiResponse<VpnMicroserviceDiagnosticsDto>.SuccessResponse(info));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpGet("get-microservice-info-by-url")]
    public async Task<ActionResult<ApiResponse<VpnMicroserviceDiagnosticsDto>>> GetMicroserviceInfoByUrl(
        [FromQuery] string baseUrl,
        [FromQuery] VpnServerType? serverType,
        CancellationToken ct)
    {
        var info = await microserviceInfoService.GetInfoByUrlAsync(baseUrl, serverType, ct);
        if (info is null)
            return NotFound(ApiResponse<VpnMicroserviceDiagnosticsDto>.ErrorResponse("Microservice info endpoint not found (404)."));
        return Ok(ApiResponse<VpnMicroserviceDiagnosticsDto>.SuccessResponse(info));
    }

    /// <summary>
    /// Legacy list (same audience as <see cref="GetAllServersWithStatus"/>). Hides Xray and UDP OpenVPN.
    /// </summary>
    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServersResponse>>> GetAllServers(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        var scope = await ResolveQuotaScopeAsync(ct);
        if (scope is null)
            return Unauthorized(ApiResponse<VpnServersResponse>.ErrorResponse("User id missing from token."));

        var cacheKey = $"v1:open-vpn-servers:get-all:legacyTcpOpenVpnOnly:ovpnProto:includeDeleted={includeDeleted}:scope={scope.CacheScopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            scope.RestrictToQuotaPlanId,
            scope.Overrides,
            ct);
        var stampKey = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";

        async Task<ApiResponse<VpnServersResponse>> BuildResponse(CancellationToken token)
        {
            var serversList = await openVpnServerQueryService.GetAll(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                scope.RestrictToQuotaPlanId,
                scope.Overrides,
                token);

            var response = serversList.Adapt<VpnServersResponse>();
            if (serversList.Count > 0)
            {
                var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(serversList.Select(s => s.Id).ToList(), token);
                for (var i = 0; i < response.VpnServers.Count; i++)
                    response.VpnServers[i].Tags = tagNamesByServer.GetValueOrDefault(serversList[i].Id, []);
            }

            response.VpnServers = await FilterLegacyTcpOpenVpnServersAsync(response.VpnServers, token);
            return ApiResponse<VpnServersResponse>.SuccessResponse(response);
        }

        ApiResponse<VpnServersResponse> cached;
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

    [HttpGet("get/{VpnServerId:int}")]
    public async Task<ActionResult<ApiResponse<VpnServerResponse>>> GetServer(
        [FromRoute] GetServerRequest request, CancellationToken ct)
    {
        var server = await openVpnServerQueryService.GetById(request.VpnServerId, ct);
        if (server == null)
            return NotFound(ApiResponse<VpnServerResponse>.ErrorResponse("VPN server not found."));
        var response = server.Adapt<VpnServerResponse>();
        response.VpnServer.Tags = await openVpnServerTagQueryService.GetTagNamesByVpnServerId(request.VpnServerId, ct);
        return Ok(ApiResponse<VpnServerResponse>.SuccessResponse(response));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPost("add")]
    public async Task<ActionResult<ApiResponse<VpnServerResponse>>> AddServer(
        [FromBody] AddServerRequest request, CancellationToken ct)
    {
        var newServer = await vpnDataService.AddVpnServer(request.Adapt<VpnServer>(), request.QuotaPlanIds, request.TagIds, ct);
        var response = newServer.Adapt<VpnServerResponse>();
        response.VpnServer.Tags = await openVpnServerTagQueryService.GetTagNamesByVpnServerId(newServer.Id, ct);
        return Ok(ApiResponse<VpnServerResponse>.SuccessResponse(response));
    }

    /// <summary>VPN node self-announce. Creates or refreshes a pending discovery row for admin approve/deny.</summary>
    [AllowAnonymous]
    [HttpPost("discover")]
    public async Task<ActionResult<ApiResponse<AnnounceVpnServerResponse>>> Discover(
        [FromBody] AnnounceVpnServerRequest request, CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!vpnServerDiscoveryService.TryAcquireAnnounceSlot(clientIp))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse<AnnounceVpnServerResponse>.ErrorResponse(VpnServerDiscoveryService.RateLimitMessage));
        }

        try
        {
            var result = await vpnServerDiscoveryService.AnnounceAsync(request, ct);
            return Ok(ApiResponse<AnnounceVpnServerResponse>.SuccessResponse(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<AnnounceVpnServerResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpGet("discoveries/pending")]
    public async Task<ActionResult<ApiResponse<VpnServerDiscoveriesResponse>>> ListPendingDiscoveries(CancellationToken ct)
    {
        var result = await vpnServerDiscoveryService.ListPendingAsync(ct);
        return Ok(ApiResponse<VpnServerDiscoveriesResponse>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPost("discoveries/{discoveryId:int}/approve")]
    public async Task<ActionResult<ApiResponse<VpnServerDiscoveryResponse>>> ApproveDiscovery(
        [FromRoute] int discoveryId,
        [FromBody] ApproveVpnServerDiscoveryRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await vpnServerDiscoveryService.ApproveAsync(discoveryId, request, ct);
            return Ok(ApiResponse<VpnServerDiscoveryResponse>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex) when (ex.Message == VpnServerDiscoveryService.NotFoundMessage)
        {
            return NotFound(ApiResponse<VpnServerDiscoveryResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<VpnServerDiscoveryResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPost("discoveries/{discoveryId:int}/deny")]
    public async Task<ActionResult<ApiResponse<VpnServerDiscoveryResponse>>> DenyDiscovery(
        [FromRoute] int discoveryId,
        [FromBody] DenyVpnServerDiscoveryRequest? request,
        CancellationToken ct)
    {
        try
        {
            var result = await vpnServerDiscoveryService.DenyAsync(discoveryId, request, ct);
            return Ok(ApiResponse<VpnServerDiscoveryResponse>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex) when (ex.Message == VpnServerDiscoveryService.NotFoundMessage)
        {
            return NotFound(ApiResponse<VpnServerDiscoveryResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<VpnServerDiscoveryResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPost("post-setup/{vpnServerId:int}/start")]
    public async Task<ActionResult<ApiResponse<VpnServerPostSetupStatusResponse>>> StartPostSetup(
        [FromRoute] int vpnServerId,
        CancellationToken ct)
    {
        var status = await vpnServerPostSetupService.StartAsync(vpnServerId, ct);
        return Ok(ApiResponse<VpnServerPostSetupStatusResponse>.SuccessResponse(ToPostSetupStatusResponse(status)));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpGet("post-setup/{vpnServerId:int}/status")]
    public async Task<ActionResult<ApiResponse<VpnServerPostSetupStatusResponse>>> GetPostSetupStatus(
        [FromRoute] int vpnServerId,
        [FromQuery] string? operationId,
        CancellationToken ct)
    {
        var status = await vpnServerPostSetupService.GetStatusAsync(vpnServerId, operationId, ct);
        if (status is null)
            return NotFound(ApiResponse<VpnServerPostSetupStatusResponse>.ErrorResponse("Post-create setup status not found."));
        return Ok(ApiResponse<VpnServerPostSetupStatusResponse>.SuccessResponse(ToPostSetupStatusResponse(status)));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPut("update")]
    public async Task<ActionResult<ApiResponse<VpnServerResponse>>> UpdateServer(
        [FromBody] UpdateServerRequest request, CancellationToken ct)
    {
        var updatedServer = await vpnDataService.UpdateVpnServer(request.Adapt<VpnServer>(),
            request.QuotaPlanIds, request.TagIds, ct);
        var response = updatedServer.Adapt<VpnServerResponse>();
        response.VpnServer.Tags = await openVpnServerTagQueryService.GetTagNamesByVpnServerId(updatedServer.Id, ct);
        return Ok(ApiResponse<VpnServerResponse>.SuccessResponse(response));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpDelete("delete/{VpnServerId:int}")]
    //todo: fixed response
    public async Task<IActionResult> DeleteServer(
        [FromRoute] DeleteServerRequest request, CancellationToken ct)
    {
        var deletedServer = await vpnDataService.DeleteVpnServer(request.VpnServerId, ct);

        return Ok(ApiResponse<bool>.SuccessResponse(deletedServer));
    }

    [HttpGet("status")]
    public ActionResult<ApiResponse<ServiceStatusesResponse>> GetStatus()
    {
        var serverStatuses = openVpnBackgroundService
            .GetStatus()
            .Select(kv => kv.Value.Adapt<ServiceStatusDto>())
            .ToList();

        var response = new ServiceStatusesResponse
        {
            ServiceStatuses = serverStatuses
        };

        return Ok(ApiResponse<ServiceStatusesResponse>.SuccessResponse(response));
    }

    [HttpPost("run-now")]
    public async Task<ActionResult<ApiResponse<string>>> RunNow(CancellationToken ct)
    {
        var serverStatuses = openVpnBackgroundService.GetStatus();

        if (serverStatuses.Values.All(x => x.Status != ServiceStatus.Running))
        {
            await openVpnBackgroundService.RunNow(ct);
        }

        return Ok(ApiResponse<string>.SuccessResponse("OpenVPN background task executed immediately."));
    }

    [HttpGet("status-stream-logs")]
    public async Task<ActionResult<ApiResponse<StatusStreamLogsResponse>>> GetStatusStreamLogs(
        [FromQuery] int limit = 300,
        CancellationToken ct = default)
    {
        var logs = await statusStreamLogStore.GetLatestAsync(limit, ct);
        var response = new StatusStreamLogsResponse
        {
            Logs = logs.Select(x => new StatusStreamLogEntryResponse
            {
                TimestampUtc = x.TimestampUtc,
                PayloadJson = x.PayloadJson,
                Source = x.Source
            }).ToList()
        };

        return Ok(ApiResponse<StatusStreamLogsResponse>.SuccessResponse(response));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpDelete("status-stream-logs")]
    public async Task<ActionResult<ApiResponse<string>>> ClearStatusStreamLogs(CancellationToken ct = default)
    {
        await statusStreamLogStore.ClearAsync(ct);
        return Ok(ApiResponse<string>.SuccessResponse("Status stream logs cleared."));
    }

    /// <returns><c>null</c> when the token carries no user id; otherwise the quota plan and personal rules to filter by.</returns>
    private async Task<QuotaScope?> ResolveQuotaScopeAsync(CancellationToken ct)
    {
        if (HttpUserContext.IsPrivileged(User))
            return new QuotaScope(null, UserVpnServerAccessOverrides.None);

        if (!HttpUserContext.TryGetUserId(User, out var userId))
            return null;

        var overrides = await userVpnServerAccessRuleQueryService.GetOverridesByUserId(userId, ct);
        var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
        return new QuotaScope(uqp?.QuotaPlanId, overrides);
    }

    private sealed record QuotaScope(int? RestrictToQuotaPlanId, UserVpnServerAccessOverrides Overrides)
    {
        public string CacheScopeKey =>
            (RestrictToQuotaPlanId is int planId ? $"plan:{planId}" : "all") + Overrides.CacheScopeSuffix;
    }

    private static VpnServerPostSetupStatusResponse ToPostSetupStatusResponse(PostSetupStatus status) =>
        new()
        {
            OperationId = status.OperationId,
            VpnServerId = status.VpnServerId,
            State = (VpnServerPostSetupState)status.State,
            Message = status.Message,
            CurrentStep = status.CurrentStep,
            StartedAtUtc = status.StartedAtUtc,
            FinishedAtUtc = status.FinishedAtUtc,
            Details = status.Details.ToDictionary(x => x.Key, x => x.Value)
        };

    private async Task<List<VpnServerWithStatusDto>> FilterLegacyTcpOpenVpnOverviewAsync(
        List<VpnServerWithStatusDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
            return items;

        var ids = items
            .Select(item => item.VpnServerResponses?.VpnServer?.Id)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var templates = await ovpnFileConfigQueryService.GetConfigTemplatesByVpnServerIds(ids, ct);
        return items
            .Where(item => item.VpnServerResponses?.VpnServer is { } server
                && LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
                    server.ServerType,
                    templates.GetValueOrDefault(server.Id)))
            .ToList();
    }

    private async Task<List<VpnServerDto>> FilterLegacyTcpOpenVpnServersAsync(
        List<VpnServerDto> servers,
        CancellationToken ct)
    {
        if (servers.Count == 0)
            return servers;

        var templates = await ovpnFileConfigQueryService.GetConfigTemplatesByVpnServerIds(
            servers.Select(s => s.Id).ToList(),
            ct);
        return servers
            .Where(server => LegacyOpenVpnV1ServerFilter.IsVisibleToLegacyTcpOpenVpnClient(
                server.ServerType,
                templates.GetValueOrDefault(server.Id)))
            .ToList();
    }

    private async Task FillTagsForOverviewResponse(VpnServerWithStatusesResponse response, CancellationToken ct)
    {
        if (response.VpnServerWithStatuses.Count == 0) return;
        var ids = response.VpnServerWithStatuses
            .Select(x => x.VpnServerResponses.VpnServer.Id)
            .ToList();
        var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(ids, ct);
        foreach (var item in response.VpnServerWithStatuses)
        {
            var id = item.VpnServerResponses.VpnServer.Id;
            item.VpnServerResponses.VpnServer.Tags = tagNamesByServer.GetValueOrDefault(id, []);
        }
    }

}
