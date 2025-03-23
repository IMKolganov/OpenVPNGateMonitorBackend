using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServerCertsController(ILogger<OpenVpnServerCertsController> logger, ICertVpnService certVpnService)
    : ControllerBase
{
    private readonly ILogger<OpenVpnServerCertsController> _logger = logger;

    [HttpGet("GetAllVpnServerCertificates/{VpnServerId:int}")]
    public async Task<IActionResult> GetAllVpnServerCertificates(
        [FromRoute] GetAllVpnServerCertificatesRequest request,
        CancellationToken cancellationToken = default)
    {
        var certificates = await certVpnService.GetAllVpnServerCertificates(request.VpnServerId, cancellationToken);
        return Ok(ApiResponse<List<VpnServerCertificateResponse>>.SuccessResponse(certificates.Adapt<List<VpnServerCertificateResponse>>()));
    }

    [HttpGet("GetAllVpnServerCertificatesByStatus/{VpnServerId:int}")]
    public async Task<IActionResult> GetAllVpnServerCertificatesByStatus(
        [FromRoute] GetAllVpnServerCertificatesByStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var certificates = await certVpnService.GetAllVpnServerCertificatesByStatus(
            request.VpnServerId, request.CertificateStatus, cancellationToken);
        return Ok(ApiResponse<List<VpnServerCertificateResponse>>.SuccessResponse(certificates.Adapt<List<VpnServerCertificateResponse>>()));
    }

    [HttpPost("AddServerCertificate")]
    public async Task<IActionResult> AddServerCertificate(
        [FromBody] AddServerCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        var certificate = await certVpnService.AddServerCertificate(
            request.VpnServerId, request.CnName, cancellationToken);

        return Ok(ApiResponse<VpnServerCertificateResponse>.SuccessResponse(certificate.Adapt<VpnServerCertificateResponse>()));
    }
    
    [HttpPost("RevokeServerCertificate")]
    public async Task<IActionResult> RevokeServerCertificate(
        [FromBody] RevokeCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await certVpnService.RevokeServerCertificate(request.VpnServerId, request.CommonName, cancellationToken);
        return Ok(ApiResponse<RevokeCertificateResponse>.SuccessResponse(result.Adapt<RevokeCertificateResponse>()));
    }

    [HttpGet("GetOpenVpnServerCertConf/{vpnServerId:int}")]
    public async Task<IActionResult> GetOpenVpnServerCertConf(
        [FromRoute] GetOpenVpnServerCertConfRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await certVpnService.GetOpenVpnServerCertConf(request.VpnServerId, cancellationToken);
        return Ok(ApiResponse<ServerCertConfigResponse>.SuccessResponse(config.Adapt<ServerCertConfigResponse>()));
    }

    [HttpPost("UpdateServerCertConfig")]
    public async Task<IActionResult> UpdateServerCertConfig(
        [FromBody] UpdateServerCertConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var updatedConfig = await certVpnService.UpdateServerCertConfig(request.Adapt<OpenVpnServerCertConfigInfo>(), cancellationToken);

        return Ok(ApiResponse<UpdateServerCertConfigResponse>.SuccessResponse(updatedConfig.Adapt<UpdateServerCertConfigResponse>()));
    }
}