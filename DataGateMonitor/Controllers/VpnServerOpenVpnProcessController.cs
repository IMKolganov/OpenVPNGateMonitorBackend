using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Api;
using DataGateMonitor.Services.Api.Interfaces;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OpenVpnProcess.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

/// <summary>Admin proxy to node manager OpenVPN daemon control (start / restart / kill / status).</summary>
[ApiController]
[Route("api/open-vpn-servers/{vpnServerId:int}/openvpn-process")]
[Authorize(Roles = "Admin")]
public class VpnServerOpenVpnProcessController(IVpnServerOpenVpnProcessService processService) : BaseController
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<OpenVpnProcessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OpenVpnProcessStatusResponse>>> Status(
        [FromRoute] int vpnServerId,
        CancellationToken ct)
    {
        try
        {
            var data = await processService.GetStatusAsync(vpnServerId, ct);
            return Ok(ApiResponse<OpenVpnProcessStatusResponse>.SuccessResponse(data, data.Message));
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<OpenVpnProcessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OpenVpnProcessStatusResponse>>> Start(
        [FromRoute] int vpnServerId,
        CancellationToken ct)
    {
        try
        {
            var data = await processService.StartAsync(vpnServerId, ct);
            return Ok(ApiResponse<OpenVpnProcessStatusResponse>.SuccessResponse(data, data.Message));
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    [HttpPost("restart")]
    [ProducesResponseType(typeof(ApiResponse<OpenVpnProcessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OpenVpnProcessStatusResponse>>> Restart(
        [FromRoute] int vpnServerId,
        CancellationToken ct)
    {
        try
        {
            var data = await processService.RestartAsync(vpnServerId, ct);
            return Ok(ApiResponse<OpenVpnProcessStatusResponse>.SuccessResponse(data, data.Message));
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    [HttpPost("kill")]
    [ProducesResponseType(typeof(ApiResponse<OpenVpnProcessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OpenVpnProcessStatusResponse>>> Kill(
        [FromRoute] int vpnServerId,
        CancellationToken ct)
    {
        try
        {
            var data = await processService.KillAsync(vpnServerId, ct);
            return Ok(ApiResponse<OpenVpnProcessStatusResponse>.SuccessResponse(data, data.Message));
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperation(ex);
        }
    }

    private static ActionResult<ApiResponse<OpenVpnProcessStatusResponse>> MapInvalidOperation(
        InvalidOperationException ex)
    {
        var body = ApiResponse<OpenVpnProcessStatusResponse>.ErrorResponse(ex.Message);
        if (IsBusy(ex.Message))
            return new ConflictObjectResult(body);
        return new BadRequestObjectResult(body);
    }

    private static bool IsBusy(string message) =>
        message.Contains("already in progress", StringComparison.OrdinalIgnoreCase);
}
