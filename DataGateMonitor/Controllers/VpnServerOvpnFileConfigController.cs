using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.Services.Cache;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerOvpnFileConfig.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerOvpnFileConfig.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/open-vpn-configs")]
[Authorize]
[Authorize(Roles = "Admin,App")]
public class VpnServerOvpnFileConfigController(
    IVpnServerOvpnFileConfigService openVpnServerOvpnFileConfigService,
    IStatusCacheGenerationService statusCacheGenerationService) : BaseController
{
    [HttpGet("get/{vpnServerId:int}")]
    public async Task<ActionResult<ApiResponse<OvpnFileConfigResponse>>> GetOvpnFileConfig(
        [FromRoute] GetOvpnFileConfigRequest request, CancellationToken cancellationToken)
    {
        var config = await openVpnServerOvpnFileConfigService
            .GetVpnServerOvpnFileConfigByServerId(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<OvpnFileConfigResponse>.SuccessResponse(config.Adapt<OvpnFileConfigResponse>()));
    }
    [HttpPost("add-update")]
    public async Task<ActionResult<ApiResponse<OvpnFileConfigResponse>>> AddOrUpdateOvpnFileConfig(
        [FromBody] AddOrUpdateOvpnFileConfigRequest request, CancellationToken ct)
    {
        if (request.VpnServerId <= 0)
            return BadRequest(ApiResponse<OvpnFileConfigResponse>.ErrorResponse("VpnServerId must be greater than 0."));
        if (string.IsNullOrWhiteSpace(request.VpnServerIp))
        {
            var resolved = await openVpnServerOvpnFileConfigService.ResolveXrayExportEndpointHostAsync(
                request.VpnServerId, ct);
            if (!string.IsNullOrWhiteSpace(resolved))
                request.VpnServerIp = resolved;
        }
        if (string.IsNullOrWhiteSpace(request.VpnServerIp))
            return BadRequest(ApiResponse<OvpnFileConfigResponse>.ErrorResponse("VpnServerIp is required."));

        var config = await openVpnServerOvpnFileConfigService
            .AddOrUpdateVpnServerOvpnFileConfigByServerId(
                request.Adapt<VpnServerOvpnFileConfig>(),
                request.AutoDetectServerSettings,
                ct);
        statusCacheGenerationService.Bump();

        return Ok(ApiResponse<OvpnFileConfigResponse>.SuccessResponse(config.Adapt<OvpnFileConfigResponse>()));
    }
}