using Microsoft.AspNetCore.Mvc;
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
    

    [HttpGet("GetAllConnectedClients")]
    public Task<IActionResult> GetAllConnectedClients(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IActionResult>(
            Ok(_certVpnService.GetAllCertVpnCertificates()));
    }
}