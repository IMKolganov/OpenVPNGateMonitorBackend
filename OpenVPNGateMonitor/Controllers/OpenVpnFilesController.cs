using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenVpnFilesController : ControllerBase
{
    private readonly ILogger<OpenVpnFilesController> _logger;
    private readonly IOvpnFileService _ovpFileService;

    public OpenVpnFilesController(ILogger<OpenVpnFilesController> logger, IOvpnFileService ovpFileService)
    {
        _logger = logger;
        _ovpFileService = ovpFileService;
    }
    
    [HttpGet("GetAllOvpnFiles")]
    public async Task<IActionResult> GetAllOvpnFiles(int vpnServerId, CancellationToken cancellationToken)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");

        return Ok(await _ovpFileService.GetAllOvpnFiles(vpnServerId, cancellationToken));
    }
    
    [HttpPost("AddOvpnFile")]
    public async Task<IActionResult> AddOvpnFile([FromBody] AddOvpnFileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.VpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        if (string.IsNullOrEmpty(request.ExternalId))
            return BadRequest("externalId is required.");
        if (string.IsNullOrEmpty(request.CommonName))
            return BadRequest("commonName is required.");

        return Ok(await _ovpFileService.AddOvpnFile(request.ExternalId, request.CommonName, request.VpnServerId, cancellationToken, request.IssuedTo));
    }
    
    [HttpPost("RevokeOvpnFile")]
    public async Task<IActionResult> RevokeOvpnFile(IssuedOvpnFile issuedOvpnFile, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _ovpFileService.RevokeOvpnFile(issuedOvpnFile, cancellationToken));
    }
}
