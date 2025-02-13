using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.Api;
using OpenVPNGateMonitor.Services.Api.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpenVpnCertsController : ControllerBase
{
    private readonly ILogger<OpenVpnCertsController> _logger;
    private readonly ICertVpnService _certVpnService;

    public OpenVpnCertsController(ILogger<OpenVpnCertsController> logger, ICertVpnService certVpnService)
    {
        _logger = logger;
        _certVpnService = certVpnService;
    }
    

    [HttpGet("GetAllVpnCertificates/{vpnServerId}")]
    public async Task<IActionResult> GetAllVpnCertificates(
        int vpnServerId, CancellationToken cancellationToken = default)
    {
        if (vpnServerId == 0)
            return BadRequest("vpnServerId is required.");
        
        return Ok(await _certVpnService.GetAllVpnCertificates(vpnServerId, cancellationToken));
    }
    
    [HttpGet("GetAllVpnCertificatesByStatus/{vpnServerId}")]
    public async Task<IActionResult> GetAllVpnCertificatesByStatus(int vpnServerId, CertificateStatus certificateStatus, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.GetAllVpnCertificatesByStatus(vpnServerId, certificateStatus, 
            cancellationToken));
    }
    
    [HttpGet("AddCertificate/{vpnServerId}")]
    public async Task<IActionResult> AddCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.AddCertificate(vpnServerId, cnName, cancellationToken));
    }
    
    [HttpGet("RemoveCertificate/{vpnServerId}")]
    public async Task<IActionResult> RemoveCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.RemoveCertificate(vpnServerId, cnName, cancellationToken));
    }
}