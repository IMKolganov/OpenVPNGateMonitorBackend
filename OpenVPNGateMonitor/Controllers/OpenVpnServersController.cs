using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServersController : ControllerBase
{
    private readonly ILogger<OpenVpnServersController> _logger;
    private readonly IVpnDataService _vpnDataService;
    private readonly ISettingsService _settingsService;
    
    private readonly IOpenVpnBackgroundService _openVpnBackgroundService;

    public OpenVpnServersController(ILogger<OpenVpnServersController> logger, IVpnDataService vpnDataService,
        IOpenVpnBackgroundService openVpnBackgroundService, ISettingsService settingsService)
    {
        _logger = logger;
        _vpnDataService = vpnDataService;
        _openVpnBackgroundService = openVpnBackgroundService;
        _settingsService = settingsService;
    }

    [HttpGet("GetAllConnectedClients/{vpnServerId}")]
    public async Task<IActionResult> GetAllConnectedClients(
        int vpnServerId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        if (page < 1 || pageSize < 1)
            return BadRequest("Invalid pagination parameters.");

        var result = await _vpnDataService.GetAllConnectedOpenVpnServerClients(vpnServerId, page, 
            pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("GetAllHistoryClients/{vpnServerId}")]
    public async Task<IActionResult> GetAllHistoryClients(
        int vpnServerId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        if (page < 1 || pageSize < 1)
            return BadRequest("Invalid pagination parameters.");

        var result = await _vpnDataService.GetAllHistoryOpenVpnServerClients(vpnServerId, page,
            pageSize, cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("GetAllServers")]
    public async Task<IActionResult> GetAllServers(CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.GetAllOpenVpnServers(cancellationToken));
    }
    
    [HttpGet("GetServerWithStats/{vpnServerId}")]
    public async Task<IActionResult> GetServerWithStats(int vpnServerId, CancellationToken cancellationToken)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        return Ok(await _vpnDataService.GetOpenVpnServerWithStats(vpnServerId, cancellationToken));
    }
    
    [HttpGet("GetServer/{vpnServerId}")]
    public async Task<IActionResult> GetServer(int vpnServerId, CancellationToken cancellationToken)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        return Ok(await _vpnDataService.GetOpenVpnServer(vpnServerId, cancellationToken));
    }
    
    [HttpPut("AddServer")]
    public async Task<IActionResult> AddServer(OpenVpnServer openVpnServer,CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.AddOpenVpnServer(openVpnServer, cancellationToken));
    }
    
    [HttpPost("UpdateServer")]
    public async Task<IActionResult> UpdateServer(OpenVpnServer openVpnServer,CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.UpdateOpenVpnServer(openVpnServer, cancellationToken));
    }
    
    [HttpDelete("DeleteServer")]
    public async Task<IActionResult> DeleteServer([FromQuery] int vpnServerId, CancellationToken cancellationToken = default)
    {
        return Ok(await _vpnDataService.DeleteOpenVpnServer(vpnServerId, cancellationToken));
    }
    
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var serverStatuses = _openVpnBackgroundService.GetStatus();

        return Ok(serverStatuses);
    }

    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken = default)
    {
        var serverStatuses = _openVpnBackgroundService.GetStatus();
        if (serverStatuses.Values.All(x => x.Status != ServiceStatus.Running))
        {
            await _openVpnBackgroundService.RunNow(cancellationToken);
        }
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
            var serverStatuses = _openVpnBackgroundService.GetStatus();

            var json = JsonConvert.SerializeObject(serverStatuses);
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(json),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            await Task.Delay(1000);
        }
    }
}
