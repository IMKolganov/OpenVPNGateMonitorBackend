using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenVpnServerController : ControllerBase
{
    private readonly ILogger<OpenVpnServerController> _logger;
    public OpenVpnServerController(ILogger<OpenVpnServerController> logger)
    {
        _logger = logger;
    }

    [HttpGet("GetAllConnectedClients")]
    public async Task<IActionResult> GetAllConnectedClients(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        // var clients = await _openVpnServerService.GetConnectedClientsAsync(cancellationToken);
        // return Ok(clients);
    }

    [HttpGet("GetServerInfo")]
    public async Task<IActionResult> GetServerInfo(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
        // var serverInfo = await _openVpnServerService.GetServerInfoAsync(cancellationToken);
        // return Ok(serverInfo);
    }

}
