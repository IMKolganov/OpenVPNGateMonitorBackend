using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenVpnFilesController : ControllerBase
{
    private readonly ILogger<OpenVpnFilesController> _logger;

    public OpenVpnFilesController(ILogger<OpenVpnFilesController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet("GetAllOvpnFiles")]
    public async Task<IActionResult> GetAllOvpnFiles(int vpnServerId, CancellationToken cancellationToken)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");

        throw new NotImplementedException();
    }
    
    [HttpPost("AddOvpnFile")]
    public async Task<IActionResult> AddOvpnFile(IssuedOvpnFile issuedOvpnFile, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    
    [HttpPost("RevokeOvpnFile")]
    public async Task<IActionResult> RevokeOvpnFile(IssuedOvpnFile issuedOvpnFile, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
