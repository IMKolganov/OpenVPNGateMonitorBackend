using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
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
    

    [HttpGet("GetAllVpnCertificates")]
    public Task<IActionResult> GetAllVpnCertificates(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.GetAllVpnCertificates()));
    }
    
    [HttpGet("GetAllVpnCertificatesByStatus")]
    public Task<IActionResult> GetAllVpnCertificatesByStatus(CertificateStatus certificateStatus, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.GetAllVpnCertificatesByStatus(certificateStatus)));
    }
}