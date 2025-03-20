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

    [HttpPost("GetAllConnectedClients")]
    public async Task<IActionResult> GetAllConnectedClients(
        [FromBody] GetConnectedClientsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllConnectedOpenVpnServerClients(
            request.VpnServerId, request.Page, request.PageSize, cancellationToken);

        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(result.Adapt<ConnectedClientsResponse>()));
    }

    [HttpPost("GetAllHistoryClients")]
    public async Task<IActionResult> GetAllHistoryClients(
        [FromBody] GetHistoryClientsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllHistoryOpenVpnServerClients(
            request.VpnServerId, request.Page, request.PageSize, cancellationToken);

        return Ok(ApiResponse<ConnectedClientsResponse>.SuccessResponse(result.Adapt<ConnectedClientsResponse>()));
    }
    
    [HttpGet("GetAllServers")]
    public async Task<IActionResult> GetAllServers(CancellationToken cancellationToken = default)
    {
        var result = await vpnDataService.GetAllOpenVpnServers(cancellationToken);
        return Ok(ApiResponse<ServersResponse>.SuccessResponse(result.Adapt<ServersResponse>()));
    }
    
    [HttpGet("GetServerWithStats/{vpnServerId:int}")]
    public async Task<IActionResult> GetServerWithStats(
        [FromRoute] GetServerWithStatsRequest request, CancellationToken cancellationToken)
    {
        var serverInfo = await vpnDataService.GetOpenVpnServerWithStats(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<ServerInfoResponse>.SuccessResponse(serverInfo.Adapt<ServerInfoResponse>()));
    }

    
    [HttpGet("GetServer/{vpnServerId:int}")]
    public async Task<IActionResult> GetServer(
        [FromRoute] GetServerRequest request, CancellationToken cancellationToken)
    {
        var server = await vpnDataService.GetOpenVpnServer(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<ServerResponse>.SuccessResponse(server.Adapt<ServerResponse>()));
    }

    [HttpPut("AddServer")]
    public async Task<IActionResult> AddServer(
        [FromBody] AddServerRequest request, CancellationToken cancellationToken = default)
    {
        var newServer = await vpnDataService.AddOpenVpnServer(request.Adapt<OpenVpnServer>(), cancellationToken);

        return Ok(ApiResponse<ServerResponse>.SuccessResponse(newServer.Adapt<ServerResponse>()));
    }
    
    [HttpPut("UpdateServer")]
    public async Task<IActionResult> UpdateServer(
        [FromBody] UpdateServerRequest request, CancellationToken cancellationToken = default)
    {
        var updatedServer = await vpnDataService.UpdateOpenVpnServer(request.Adapt<OpenVpnServer>(), cancellationToken);

        return Ok(ApiResponse<ServerResponse>.SuccessResponse(updatedServer.Adapt<ServerResponse>()));
    }

    [HttpDelete("DeleteServer/{vpnServerId:int}")]
    public async Task<IActionResult> DeleteServer(
        [FromRoute] DeleteServerRequest request, CancellationToken cancellationToken = default)
    {
        var deletedServer = await vpnDataService.DeleteOpenVpnServer(request.VpnServerId, cancellationToken);

        return Ok(ApiResponse<ServerResponse>.SuccessResponse(deletedServer.Adapt<ServerResponse>()));
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
        while (webSocket.State == WebSocketState.Open)
        {
            var serverStatuses = openVpnBackgroundService.GetStatus()
                .Select(x => x.Adapt<ServiceStatusResponse>())
                .ToList();

            var json = JsonConvert.SerializeObject(ApiResponse<List<ServiceStatusResponse>>.SuccessResponse(serverStatuses));
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(json),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            await Task.Delay(1000);
        }
    }
}
