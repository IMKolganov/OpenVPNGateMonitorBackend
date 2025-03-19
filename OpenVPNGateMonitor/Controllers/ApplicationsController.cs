using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Auth;
using OpenVPNGateMonitor.SharedModels.Applications.Requests;
using OpenVPNGateMonitor.SharedModels.Applications.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ApplicationsController(IApplicationService appService) : ControllerBase
{
    [HttpPost("RegisterApplication")]
    public async Task<IActionResult> RegisterApplication([FromBody] RegisterApplicationRequest request)
    {
        var newApp = await appService.RegisterApplicationAsync(request.Name);
        
        return Ok(ApiResponse<RegisterApplicationResponse>.SuccessResponse(newApp.Adapt<RegisterApplicationResponse>()));
    }

    [HttpGet("GetAllApplications")]
    public async Task<IActionResult> GetAllApplications()
    {
        var apps = await appService.GetAllApplicationsAsync();
        return Ok(ApiResponse<List<ApplicationDto>>.SuccessResponse(apps.Adapt<List<ApplicationDto>>()));
    }

    [HttpPost("RevokeApplication")]
    public async Task<IActionResult> RevokeApplication([FromBody] RevokeApplicationRequest request)
    {
        var result = await appService.RevokeApplicationAsync(request.ClientId);
        
        if (!result)
            return NotFound(ApiResponse<string>.ErrorResponse("Application not found"));

        return Ok(ApiResponse<string>.SuccessResponse("Application revoked"));
    }
}
