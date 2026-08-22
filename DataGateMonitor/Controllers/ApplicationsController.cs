using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[Route("api/applications")]
[ApiController]
[Authorize(Roles = "Admin")]
public class ApplicationsController(IApplicationService appService) : BaseController
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterApplicationResponse>>> RegisterApplication([FromBody] 
        RegisterApplicationRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var newApp = await appService.RegisterApplicationAsync(request.Name, cancellationToken);

            return Ok(ApiResponse<RegisterApplicationResponse>.SuccessResponse(
                newApp.Adapt<RegisterApplicationResponse>()));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<RegisterApplicationResponse>.ErrorResponse(ex.Message));
        }
    }

    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<ApplicationsResponse>>> GetAllApplications(
        [FromQuery] GetAllApplicationsRequest request,
        CancellationToken cancellationToken)
    {
        var apps = await appService.GetAllApplicationsAsync(request, cancellationToken);

        var dtoList = apps.Adapt<List<ApplicationDto>>();

        var response = new ApplicationsResponse
        {
            Applications = dtoList
        };

        return Ok(ApiResponse<ApplicationsResponse>.SuccessResponse(response));
    }


    [HttpPost("revoke")]
    public async Task<ActionResult<ApiResponse<string>>> RevokeApplication(
        [FromBody] RevokeApplicationRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await appService.RevokeApplicationAsync(request.ClientId, cancellationToken);

            if (!result)
                return NotFound(ApiResponse<string>.ErrorResponse("Application not found"));

            return Ok(ApiResponse<string>.SuccessResponse("Application revoked"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.ErrorResponse(ex.Message));
        }
    }
}