﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeoLiteController : ControllerBase
{
    private readonly ILogger<GeoLiteController> _logger;
    private readonly IGeoLiteQueryService _geoLiteQueryService;
    private readonly IGeoLiteUpdaterService _geoLiteUpdaterService;

    public GeoLiteController(
        ILogger<GeoLiteController> logger,
        IGeoLiteQueryService geoLiteQueryService,
        IGeoLiteUpdaterService geoLiteUpdaterService)
    {
        _logger = logger;
        _geoLiteQueryService = geoLiteQueryService;
        _geoLiteUpdaterService = geoLiteUpdaterService;
    }
    
    [HttpGet("GetDatabasePath")]
    public async Task<IActionResult> GetDatabasePath(CancellationToken cancellationToken = default)
    {
        var dbPath = await _geoLiteQueryService.GetDatabasePathAsync();
        return Ok(new { databasePath = dbPath });
    }
    
    [HttpGet("GetGeoInfo")]
    public async Task<IActionResult> GetGeoInfo(string ipaddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ipaddress))
            return BadRequest("ipaddress is required.");

        var geoInfo = await _geoLiteQueryService.GetGeoInfoAsync(ipaddress, cancellationToken);
        if (geoInfo == null)
            return NotFound(new { message = "No geo information found for the provided IP address." });

        return Ok(geoInfo);
    }
    
    [HttpGet("GetVersionDatabase")]
    public async Task<IActionResult> GetVersionDatabase(CancellationToken cancellationToken = default)
    {
        var version = await _geoLiteQueryService.GetDatabaseVersionAsync(cancellationToken);
        return Ok(new { version });
    }

    [HttpPost("UpdateDatabase")]
    public async Task<IActionResult> UpdateDatabase(CancellationToken cancellationToken = default)
    {
        var updateResult = await _geoLiteUpdaterService.DownloadAndUpdateDatabaseAsync(cancellationToken);

        if (!updateResult.Success)
            return BadRequest(new { message = "Database update failed", error = updateResult.ErrorMessage });

        return Ok(updateResult);
    }
}
