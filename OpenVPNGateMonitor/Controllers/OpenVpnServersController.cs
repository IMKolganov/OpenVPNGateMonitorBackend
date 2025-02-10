using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenVpnServersController : ControllerBase
{
    private readonly ILogger<OpenVpnServersController> _logger;
    private readonly IVpnDataService _vpnDataService;
    
    private readonly IOpenVpnBackgroundService _openVpnBackgroundService;

    public OpenVpnServersController(ILogger<OpenVpnServersController> logger, IVpnDataService vpnDataService,
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
    
    
    [HttpGet("GetAllHistoryClients")]
    public async Task<IActionResult> GetAllHistoryClients(CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.GetAllHistoryOpenVpnServerClients(cancellationToken));
    }
    
    [HttpGet("GetAllServers")]
    public async Task<IActionResult> GetAllServers(CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.GetAllOpenVpnServers(cancellationToken));
    }
    
    [HttpPost("AddServer")]
    public async Task<IActionResult> AddServer(OpenVpnServer openVpnServer,CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.AddOpenVpnServer(openVpnServer, cancellationToken));
    }
    
    [HttpGet("DeleteServer")]
    public async Task<IActionResult> DeleteServer(OpenVpnServer openVpnServer, CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.DeleteOpenVpnServer(openVpnServer, cancellationToken));
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
