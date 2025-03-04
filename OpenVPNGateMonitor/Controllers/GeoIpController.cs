using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeoIpController : ControllerBase
{
    private readonly ILogger<GeoIpController> _logger;
    private readonly IGeoIpService _geoIpService;

    public GeoIpController(ILogger<GeoIpController> logger, IGeoIpService geoIpService)
    {
        _logger = logger;
        _geoIpService = geoIpService;
    }
    

    [HttpGet("GetDatabasePath")]
    public IActionResult GetDatabasePath(CancellationToken cancellationToken = default)
    {
        return Ok(_geoIpService.GetDataBasePath(cancellationToken));
    }
    
    [HttpGet("GetGeoInfo")]
    public IActionResult GetGeoInfo(
        string ipaddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ipaddress))
            return BadRequest("vpnServerId is required.");
        
        return Ok(_geoIpService.GetGeoInfo(ipaddress)!);
    }
    
    [HttpGet("GetVersionDatabase")]
    public async Task<IActionResult> GetVersionDatabase(CancellationToken cancellationToken = default)
    {
        var version = await _geoIpService.GetDatabaseVersionAsync(cancellationToken);
        return Ok(new { version });
    }

    [HttpPost("UpdateDatabase")]
    public async Task<IActionResult> UpdateDatabase(CancellationToken cancellationToken = default)
    {
        await _geoIpService.DownloadAndUpdateDatabaseAsync(cancellationToken);
        return Ok(new { message = "Database updated!" });
    }
}