using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet.Subscribers;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/openvpn")]
[ApiController]
public class OpenVpnWebSocketController : ControllerBase
{
    private readonly IOpenVpnTelnetService _openVpnTelnetService;

    public OpenVpnWebSocketController(IOpenVpnTelnetService openVpnTelnetService)
    {
        _openVpnTelnetService = openVpnTelnetService;
    }
    
    [HttpGet("ws/{openVpnServerId}")]
    public async Task GetWebSocket(int openVpnServerId)
    {
        await _openVpnTelnetService.HandleWebSocketByServerIdAsync(HttpContext, openVpnServerId);
    }


    [HttpGet("ws/{ip}/{port}")]
    public async Task GetWebSocket(string ip, int port)
    {
        await _openVpnTelnetService.HandleWebSocketAsync(HttpContext, ip, port);
    }

}