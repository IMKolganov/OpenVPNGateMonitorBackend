using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServerOvpnFileConfigController : ControllerBase
{
    private readonly IOpenVpnServerOvpnFileConfigService _openVpnServerOvpnFileConfigService;

    public OpenVpnServerOvpnFileConfigController(IOpenVpnServerOvpnFileConfigService openVpnServerOvpnFileConfigService)
    {
        _openVpnServerOvpnFileConfigService = openVpnServerOvpnFileConfigService;
    }
    
    [HttpGet("GetOvpnFileConfig/{vpnServerId}")]
    public async Task<IActionResult> GetOvpnFileConfig(
        int vpnServerId, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");

        return Ok(await _openVpnServerOvpnFileConfigService.GetOpenVpnServerOvpnFileConfigByServerId(vpnServerId,
            cancellationToken));
    }
    
    [HttpPost("AddOrUpdateOvpnFileConfig")]
    public async Task<IActionResult> AddOrUpdateOvpnFileConfig(OpenVpnServerOvpnFileConfig openVpnServerOvpnFileConfig,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _openVpnServerOvpnFileConfigService.AddOrUpdateOpenVpnServerOvpnFileConfigByServerId(
            openVpnServerOvpnFileConfig, cancellationToken));
    }
}
