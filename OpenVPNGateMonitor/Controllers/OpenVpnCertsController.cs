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
    
    [HttpGet("RemoveServerCertificate/{vpnServerId}")]
    public async Task<IActionResult> RemoveServerCertificate(int vpnServerId, string cnName, 
        CancellationToken cancellationToken = default)
    {
        return Ok(await _certVpnService.RemoveServerCertificate(vpnServerId, cnName, cancellationToken));
    }
}