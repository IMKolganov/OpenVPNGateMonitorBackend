using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTagTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Auth.Handlers.Interfaces;
using DataGateMonitor.Services.Api.Interfaces;
using PostSetupStatus = DataGateMonitor.Services.Api.PostSetup.VpnServerPostSetupStatus;
using DataGateMonitor.Services.BackgroundServices.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.StatusStreamLogs;
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
    IVpnServerOverviewQuery openVpnServerOverviewQuery, IVpnServerQueryService openVpnServerQueryService,
    IVpnServerTagQueryService openVpnServerTagQueryService,
    IOpenVpnBackgroundService openVpnBackgroundService,
    IMicroserviceInfoService microserviceInfoService,
    IUserQuotaPlanQueryService userQuotaPlanQueryService,
    IVpnServerAccessQueryService vpnServerAccessQueryService,
    IApiMemoryCacheService apiMemoryCacheService,
    IStatusCacheGenerationService statusCacheGenerationService,
    IStatusStreamLogStore statusStreamLogStore,
    IVpnServerPostSetupService vpnServerPostSetupService,
    IConnectedClientsCounterStore connectedClientsCounterStore) : BaseController
{
    private static readonly TimeSpan ServersListCacheTtl = TimeSpan.FromHours(1);

    [HttpGet("get-all-with-status")]
    public async Task<ActionResult> GetAllServersWithStatus(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        int? restrictToQuotaPlanId;
        if (HttpUserContext.IsPrivileged(User))
        {
            restrictToQuotaPlanId = null;
        }
        else
        {
            if (!HttpUserContext.TryGetUserId(User, out var userId))
                return Unauthorized(ApiResponse<VpnServerWithStatusesResponse>.ErrorResponse("User id missing from token."));
            var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
            restrictToQuotaPlanId = uqp?.QuotaPlanId;
        }

        var scopeKey = restrictToQuotaPlanId is int planId ? $"plan:{planId}" : "all";
        var cacheKey = $"v1:open-vpn-servers:get-all-with-status:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId,
            ct);
        var dataStamp = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";
        var stampKey = $"{dataStamp}:status:{statusCacheGenerationService.CurrentStamp}";
        async Task<string> BuildPayload(CancellationToken token)
        {
            var result = await openVpnServerOverviewQuery.GetAllVpnServersWithStatusAsync(
                includeDeleted, requireQuotaPlanAssignment: false, restrictToQuotaPlanId, token);
            await VpnServerConnectedCountOverlay.ApplyAsync(result, connectedClientsCounterStore, token);

            var baseResponse = new VpnServerWithStatusesResponse
            {
                VpnServerWithStatuses = result
            };
            await FillTagsForOverviewResponse(baseResponse, token);

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
        var response = serverInfo.Adapt<VpnServerWithStatusResponse>();
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

    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServersResponse>>> GetAllServers(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default,
        [FromQuery] bool withoutCache = false)
    {
        int? restrictToQuotaPlanId;
        if (HttpUserContext.IsPrivileged(User))
        {
            restrictToQuotaPlanId = null;
        }
        else
        {
            if (!HttpUserContext.TryGetUserId(User, out var userId))
                return Unauthorized(ApiResponse<VpnServersResponse>.ErrorResponse("User id missing from token."));
            var uqp = await userQuotaPlanQueryService.GetActiveByUserId(userId, ct);
            restrictToQuotaPlanId = uqp?.QuotaPlanId;
        }

        var scopeKey = restrictToQuotaPlanId is int planId ? $"plan:{planId}" : "all";
        var cacheKey = $"v1:open-vpn-servers:get-all:includeDeleted={includeDeleted}:scope={scopeKey}";
        var stamp = await openVpnServerQueryService.GetLastUpdateStamp(
            includeDeleted,
            requireQuotaPlanAssignment: false,
            restrictToQuotaPlanId,
            ct);
        var stampKey = stamp?.ToUnixTimeMilliseconds().ToString() ?? "empty";

        async Task<ApiResponse<VpnServersResponse>> BuildResponse(CancellationToken token)
        {
            var serversList = await openVpnServerQueryService.GetAll(
                includeDeleted,
                requireQuotaPlanAssignment: false,
                restrictToQuotaPlanId,
                token);

            var response = serversList.Adapt<VpnServersResponse>();
            if (serversList.Count > 0)
            {
                var tagNamesByServer = await openVpnServerTagQueryService.GetTagNamesByVpnServerIds(serversList.Select(s => s.Id).ToList(), token);
                for (var i = 0; i < response.VpnServers.Count; i++)
                    response.VpnServers[i].Tags = tagNamesByServer.GetValueOrDefault(serversList[i].Id, []);
            }
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
