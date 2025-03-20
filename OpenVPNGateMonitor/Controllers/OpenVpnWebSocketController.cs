using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnWebSocket.Requests;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/openvpn")]
[ApiController]
[Authorize]
public class OpenVpnWebSocketController(IOpenVpnTelnetService openVpnTelnetService) : ControllerBase
{
    [HttpGet("ws/{openVpnServerId:int}")]
    public async Task GetWebSocket(
        [FromRoute] GetWebSocketByServerIdRequest request,
        CancellationToken cancellationToken = default)
    {
        await openVpnTelnetService.HandleWebSocketByServerIdAsync(HttpContext, request.OpenVpnServerId, cancellationToken);
    }

    [HttpGet("ws/{ip}/{port:int}")]
    public async Task GetWebSocket(
        [FromRoute] GetWebSocketByIpRequest request,
        CancellationToken cancellationToken = default)
    {
        await openVpnTelnetService.HandleWebSocketAsync(HttpContext, request.Ip, request.Port, cancellationToken);
    }
}