using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnFilesController(IOvpnFileService ovpFileService) : ControllerBase
{
    [HttpGet("GetAllOvpnFiles")]
    public async Task<IActionResult> GetAllOvpnFiles([FromRoute] GetAllOvpnFilesRequest request,
        CancellationToken cancellationToken)
    {
        var files = await ovpFileService.GetAllOvpnFiles(request.VpnServerId, cancellationToken);
        var response = files.Adapt<List<OvpnFileResponse>>();

        return Ok(ApiResponse<List<OvpnFileResponse>>.SuccessResponse(response));
    }

    [HttpGet("GetAllByExternalIdOvpnFiles")]
    public async Task<IActionResult> GetAllByExternalIdOvpnFiles([FromRoute] GetAllByExternalIdOvpnFilesRequest request,
        CancellationToken cancellationToken)
    {
        var files = await ovpFileService.GetAllOvpnFilesByExternalId(
            request.VpnServerId, request.ExternalId, cancellationToken);

        return Ok(ApiResponse<List<OvpnFileResponse>>.SuccessResponse(files.Adapt<List<OvpnFileResponse>>()));
    }

    [HttpPost("AddOvpnFile")]
    public async Task<IActionResult> AddOvpnFile([FromBody] AddOvpnFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var file = await ovpFileService.AddOvpnFile(request.ExternalId, request.CommonName, request.VpnServerId,
            cancellationToken, request.IssuedTo);
        return Ok(ApiResponse<OvpnFileResponse>.SuccessResponse(file.Adapt<OvpnFileResponse>(),
            "Ovpn file added successfully."));
    }

    [HttpPost("RevokeOvpnFile")]
    public async Task<IActionResult> RevokeOvpnFile([FromBody] RevokeOvpnFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var issuedOvpnFile = request.Adapt<IssuedOvpnFile>();

        var result = await ovpFileService.RevokeOvpnFile(issuedOvpnFile, cancellationToken);

        if (result != null)
            return NotFound(
                ApiResponse<RevokeOvpnFileResponse>.ErrorResponse("File not found or already revoked."));

        return Ok(ApiResponse<RevokeOvpnFileResponse>.SuccessResponse(new RevokeOvpnFileResponse
        {
            Success = true,
            Message = "Ovpn file revoked successfully."
        }));
    }

    [HttpGet("DownloadOvpnFile/{issuedOvpnFileId}/{vpnServerId}")]
    public async Task<IActionResult> DownloadOvpnFile(
        [FromRoute] DownloadOvpnFileRequest request,
        CancellationToken cancellationToken)
    {
        var issuedOvpnFileResult = await ovpFileService.GetOvpnFile(
            request.IssuedOvpnFileId, request.VpnServerId, cancellationToken);

        if (issuedOvpnFileResult.FileStream == null)
            return NotFound(ApiResponse<string>.ErrorResponse($"File not found: {issuedOvpnFileResult.FileName}"));

        var response = issuedOvpnFileResult.Adapt<DownloadOvpnFileResponse>();

        var safeFileName = Uri.EscapeDataString(response.FileName);
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeFileName}\"";
        Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";

        return File(response.FileStream, "application/x-openvpn-profile", response.FileName);
    }
}
