using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenVpnServerController : ControllerBase
{
    private readonly ILogger<OpenVpnServerController> _logger;
    private readonly IOpenVpnServerService _openVpnServerService;
    public OpenVpnServerController(ILogger<OpenVpnServerController> logger, IOpenVpnServerService openVpnServerService)
    {
        _logger = logger;
        _openVpnServerService = openVpnServerService;
    }

    [HttpGet("GetAllConnectedClients")]
    public async Task<IActionResult> GetAllConnectedClients(CancellationToken cancellationToken = default)
    {
        var clients = await _openVpnServerService.GetConnectedClientsAsync(cancellationToken);
        return Ok(clients);
    }

    [HttpGet("GetServerInfo")]
    public async Task<IActionResult> GetServerInfo(CancellationToken cancellationToken = default)
    {
        var serverInfo = await _openVpnServerService.GetServerInfoAsync(cancellationToken);
        return Ok(serverInfo);
    }

}
