using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

[ApiController]
[Route("api/OpenVpnWebSocket")]
public class OpenVpnWebSocketController : ControllerBase
{
    private readonly OpenVpnManagerPool _openVpnManagerPool;
    private readonly ILogger<OpenVpnWebSocketController> _logger;

    public OpenVpnWebSocketController(OpenVpnManagerPool openVpnManagerPool, ILogger<OpenVpnWebSocketController> logger)
    {
        _logger = logger;
        _openVpnManagerPool = openVpnManagerPool;
    }

    [HttpGet("ws/{ip}/{port}")]
    public async Task GetWebSocket(string ip, int port)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation($"WebSocket connection established for {ip}:{port}");

            await _openVpnManagerPool.HandleWebSocketAsync(webSocket, ip, port, HttpContext.RequestAborted);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}