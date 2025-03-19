using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServerOvpnFileConfigController(
    IOpenVpnServerOvpnFileConfigService openVpnServerOvpnFileConfigService)
    : ControllerBase
{
    [HttpGet("GetOvpnFileConfig")]
    public async Task<IActionResult> GetOvpnFileConfig(
        [FromBody] int vpnServerId, CancellationToken cancellationToken = default)
    {
        var config = await openVpnServerOvpnFileConfigService
            .GetOpenVpnServerOvpnFileConfigByServerId(vpnServerId, cancellationToken);

        return Ok(ApiResponse<OvpnFileConfigResponse>.SuccessResponse(config.Adapt<OvpnFileConfigResponse>()));
    }
    
    [HttpPost("AddOrUpdateOvpnFileConfig")]
    public async Task<IActionResult> AddOrUpdateOvpnFileConfig(
        [FromBody] AddOrUpdateOvpnFileConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = await openVpnServerOvpnFileConfigService
            .AddOrUpdateOpenVpnServerOvpnFileConfigByServerId(request.Adapt<OpenVpnServerOvpnFileConfig>(), cancellationToken);

        return Ok(ApiResponse<OvpnFileConfigResponse>.SuccessResponse(config.Adapt<OvpnFileConfigResponse>()));
    }
}
