using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/openvpn")]
[ApiController]
[Authorize]
public class OpenVpnWebSocketController : ControllerBase
{
    private readonly IOpenVpnTelnetService _openVpnTelnetService;

    public OpenVpnWebSocketController(IOpenVpnTelnetService openVpnTelnetService)
    {
        _openVpnTelnetService = openVpnTelnetService;
    }
    
    [HttpGet("ws/{openVpnServerId}")]
    public async Task GetWebSocket(int openVpnServerId, CancellationToken cancellationToken = default)
    {
        await _openVpnTelnetService.HandleWebSocketByServerIdAsync(HttpContext, openVpnServerId, cancellationToken);
    }


    [HttpGet("ws/{ip}/{port}")]
    public async Task GetWebSocket(string ip, int port, CancellationToken cancellationToken = default)
    {
        await _openVpnTelnetService.HandleWebSocketAsync(HttpContext, ip, port, cancellationToken);
    }

}