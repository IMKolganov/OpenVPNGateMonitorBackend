using System.Net.WebSockets;
using System.Text;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnServers.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServersController(
    ILogger<OpenVpnServersController> logger,
    IVpnDataService vpnDataService,
    IOpenVpnBackgroundService openVpnBackgroundService)
    : ControllerBase
{
    private readonly ILogger<OpenVpnServersController> _logger = logger;
    private static bool _isStatusUpdateRunning = false;

    [HttpGet("GetAllConnectedClients")]
    public async Task<IActionResult> GetAllConnectedClients(
        [FromQuery] GetConnectedClientsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllConnectedOpenVpnServerClients(
            request.VpnServerId, request.Page, request.PageSize, cancellationToken);

        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(result.Adapt<ConnectedClientsResponse>()));
    }

    [HttpGet("GetAllHistoryClients")]
    public async Task<IActionResult> GetAllHistoryClients(
        [FromQuery] GetHistoryClientsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllHistoryOpenVpnServerClients(
            request.VpnServerId, request.Page, request.PageSize, cancellationToken);

        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(result.Adapt<ConnectedClientsResponse>()));
    }
    
    [HttpGet("GetAllServersWithStatus")]
    public async Task<IActionResult> GetAllServersWithStatus(CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllOpenVpnServersWithStatus(cancellationToken);
        return Ok(ApiResponse<List<OpenVpnServerWithStatusResponse>>.SuccessResponse(result.Adapt<List<OpenVpnServerWithStatusResponse>>()));
    }
    
    [HttpGet("GetServerWithStatus/{vpnServerId:int}")]
    public async Task<IActionResult> GetServerWithStatus(
        [FromRoute] GetServerWithStatsRequest request, CancellationToken cancellationToken)
    {
        var serverInfo = await vpnDataService.GetOpenVpnServerWithStatus(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<OpenVpnServerWithStatusResponse>.SuccessResponse(serverInfo.Adapt<OpenVpnServerWithStatusResponse>()));
    }

    
    [HttpGet("GetServer/{vpnServerId:int}")]
    public async Task<IActionResult> GetServer(
        [FromRoute] GetServerRequest request, CancellationToken cancellationToken)
    {
        var server = await vpnDataService.GetOpenVpnServer(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<OpenVpnServerResponse>.SuccessResponse(server.Adapt<OpenVpnServerResponse>()));
    }

    [HttpPost("AddServer")]
    public async Task<IActionResult> AddServer(
        [FromBody] AddServerRequest request, CancellationToken cancellationToken = default)
    {
        var newServer = await vpnDataService.AddOpenVpnServer(request.Adapt<OpenVpnServer>(), cancellationToken);

        return Ok(ApiResponse<OpenVpnServerResponse>.SuccessResponse(newServer.Adapt<OpenVpnServerResponse>()));
    }
    
    [HttpPut("UpdateServer")]
    public async Task<IActionResult> UpdateServer(
        [FromBody] UpdateServerRequest request, CancellationToken cancellationToken = default)
    {
        var updatedServer = await vpnDataService.UpdateOpenVpnServer(request.Adapt<OpenVpnServer>(), cancellationToken);

        return Ok(ApiResponse<OpenVpnServerResponse>.SuccessResponse(updatedServer.Adapt<OpenVpnServerResponse>()));
    }

    [HttpDelete("DeleteServer/{vpnServerId:int}")]
    public async Task<IActionResult> DeleteServer(
        [FromRoute] DeleteServerRequest request, CancellationToken cancellationToken = default)
    {
        var deletedServer = await vpnDataService.DeleteOpenVpnServer(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<bool>.SuccessResponse(deletedServer));
    }
    
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var serverStatuses = openVpnBackgroundService.GetStatus()
            .Select(x => x.Adapt<ServiceStatusResponse>())
            .ToList();

        return Ok(ApiResponse<List<ServiceStatusResponse>>.SuccessResponse(serverStatuses));
    }

    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken = default)
    {
        var serverStatuses = openVpnBackgroundService.GetStatus();

        if (serverStatuses.Values.All(x => x.Status != ServiceStatus.Running))
        {
            await openVpnBackgroundService.RunNow(cancellationToken);
        }

        return Ok(ApiResponse<string>.SuccessResponse("OpenVPN background task executed immediately."));
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
        if (_isStatusUpdateRunning)
            return;

        _isStatusUpdateRunning = true;

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var statuses = openVpnBackgroundService.GetStatus().Values
                    .Select(x => x.Adapt<ServiceStatusResponse>())
                    .ToList();

                var json = JsonConvert.SerializeObject(statuses);
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(json),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);

                await Task.Delay(1000);
            }
        }
        finally
        {
            _isStatusUpdateRunning = false;
        }
    }
}
