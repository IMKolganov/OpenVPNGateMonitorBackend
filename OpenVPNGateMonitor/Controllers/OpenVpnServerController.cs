using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenVpnServerController : ControllerBase
{
    private readonly ILogger<OpenVpnServerController> _logger;
    private readonly IVpnDataService _vpnDataService;
    private readonly IOpenVpnBackgroundService _openVpnBackgroundService;

    public OpenVpnServerController(ILogger<OpenVpnServerController> logger, IVpnDataService vpnDataService,
        IOpenVpnBackgroundService openVpnBackgroundService)
    {
        _logger = logger;
        _vpnDataService = vpnDataService;
        _openVpnBackgroundService = openVpnBackgroundService;
    }

    [HttpGet("GetAllConnectedClients")]
    public async Task<IActionResult> GetAllConnectedClients(CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.GetAllConnectedOpenVpnServerClients(cancellationToken));
    }

    [HttpGet("GetServerInfo")]
    public async Task<IActionResult> GetServerInfo(CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.GetOpenVpnServerStatusLog(cancellationToken));
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            nextRunTime = _openVpnBackgroundService.GetNextRunTime(),
            status = _openVpnBackgroundService.GetStatus().ToString()
        });
    }

    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
    {
        await _openVpnBackgroundService.RunNow(cancellationToken);
        return Ok(new { message = "OpenVPN background task executed immediately." });
    }
    
    [HttpGet("status-stream")]
    public async Task StatusStream()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await SendStatusUpdates(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task SendStatusUpdates(WebSocket webSocket)
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var statusUpdate = new
            {
                status = _openVpnBackgroundService.GetStatus().ToString(),
                nextRunTime = _openVpnBackgroundService.GetNextRunTime()
            };

            string json = JsonConvert.SerializeObject(statusUpdate);
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(json),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            await Task.Delay(1000);
        }
    }
}
