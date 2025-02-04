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
    
    // [HttpGet("GetAllVpnCertificates")]
    // public Task<IActionResult> GetAllVpnCertificates(CancellationToken cancellationToken = default)
    // {
    //     var certificates = new List<CertificateCaInfo>
    //     {
    //         new CertificateCaInfo
    //         {
    //             Status = CertificateStatus.Active,
    //             ExpiryDate = DateTime.UtcNow.AddMonths(6),
    //             SerialNumber = "123ABC",
    //             CommonName = "client1"
    //         },
    //         new CertificateCaInfo
    //         {
    //             Status = CertificateStatus.Revoked,
    //             ExpiryDate = DateTime.UtcNow.AddMonths(-3),
    //             SerialNumber = "456DEF",
    //             CommonName = "client2"
    //         },
    //         new CertificateCaInfo
    //         {
    //             Status = CertificateStatus.Active,
    //             ExpiryDate = DateTime.UtcNow.AddYears(1),
    //             SerialNumber = "789GHI",
    //             CommonName = "client3"
    //         }
    //     };
    //
    //     return Task.FromResult<IActionResult>(Ok(certificates));
    // }
}