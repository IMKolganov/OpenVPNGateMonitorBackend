using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.Api.Auth;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/applications")]
[ApiController]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _appService;

    public ApplicationsController(IApplicationService appService)
    {
        _appService = appService;
    }

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> RegisterApplication([FromBody] RegisterAppRequest request)
    {
        var newApp = await _appService.RegisterApplicationAsync(request.Name);
        return Ok(new { clientId = newApp.ClientId, clientSecret = newApp.ClientSecret });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllApplications()
    {
        var apps = await _appService.GetAllApplicationsAsync();
        return Ok(apps);
    }

    [HttpPost("revoke/{clientId}")]
    [Authorize]
    public async Task<IActionResult> RevokeApplication(string clientId)
    {
        var result = await _appService.RevokeApplicationAsync(clientId);
        if (!result) return NotFound(new { message = "Application not found" });

        return Ok(new { message = "Application revoked" });
    }
}
