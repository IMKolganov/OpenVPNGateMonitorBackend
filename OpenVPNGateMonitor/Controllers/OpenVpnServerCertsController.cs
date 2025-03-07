﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OpenVpnServerCertsController : ControllerBase
{
    private readonly ILogger<OpenVpnServerCertsController> _logger;
    private readonly ICertVpnService _certVpnService;

    public OpenVpnServerCertsController(ILogger<OpenVpnServerCertsController> logger, ICertVpnService certVpnService)
    {
        _logger = logger;
        _certVpnService = certVpnService;
    }
    

    [HttpGet("GetAllVpnServerCertificates/{vpnServerId}")]
    public async Task<IActionResult> GetAllVpnServerCertificates(
        int vpnServerId, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        
        return Ok(await _certVpnService.GetAllVpnServerCertificates(vpnServerId, cancellationToken));
    }
    
    [HttpGet("GetAllVpnServerCertificatesByStatus/{vpnServerId}")]
    public async Task<IActionResult> GetAllVpnServerCertificatesByStatus(int vpnServerId, CertificateStatus certificateStatus, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.GetAllVpnServerCertificatesByStatus(vpnServerId, certificateStatus, 
            cancellationToken));
    }
    
    [HttpGet("AddServerCertificate/{vpnServerId}")]
    public async Task<IActionResult> AddServerCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.AddServerCertificate(vpnServerId, cnName, cancellationToken));
    }
    
    [HttpPost("RevokeServerCertificate")]
    public async Task<IActionResult> RevokeServerCertificate(
        [FromBody] RevokeCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.VpnServerId <= 0 || string.IsNullOrWhiteSpace(request.CommonName))//todo: make validation
        {
            return BadRequest("Invalid request. 'VpnServerId' & 'CnName' are required.");
        }

        var result = await _certVpnService.RevokeServerCertificate(request.VpnServerId, request.CommonName, cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("GetOpenVpnServerCertConf/{vpnServerId:int}")]
    public async Task<IActionResult> GetOpenVpnServerCertConf(int vpnServerId, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");

        return Ok( await _certVpnService.GetOpenVpnServerCertConf(vpnServerId, cancellationToken));
    }
    
    [HttpPost("UpdateServerCertConfig")]
    public async Task<IActionResult> UpdateServerCertConfig(OpenVpnServerCertConfigRequest request, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.UpdateServerCertConfig(request, cancellationToken));
    }
}