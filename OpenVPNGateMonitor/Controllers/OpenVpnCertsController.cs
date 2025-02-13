using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Services.Api;
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
        
        return Task.FromResult<IActionResult>(
            Ok(await _certVpnService.GetAllVpnCertificates()));
    }
    
    [HttpGet("GetAllVpnCertificatesByStatus")]
    public Task<IActionResult> GetAllVpnCertificatesByStatus(CertificateStatus certificateStatus, 
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.GetAllVpnCertificatesByStatus(certificateStatus)));
    }
    
    [HttpGet("AddCertificate")]
    public Task<IActionResult> AddCertificate(string cnName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.AddCertificate(cnName, cancellationToken)));
    }
    
    [HttpGet("RemoveCertificate")]
    public Task<IActionResult> RemoveCertificate(string cnName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.RemoveCertificate(cnName, cancellationToken)));
    }
}