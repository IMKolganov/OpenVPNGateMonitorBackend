using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataGateMonitor.Services.VpnServerGroups;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServerGroups.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[ApiController]
[Route("api/vpn-server-groups")]
[Authorize]
public class VpnServerGroupsController(IVpnServerGroupService groupService) : BaseController
{
    [HttpGet("get-all")]
    public async Task<ActionResult<ApiResponse<VpnServerGroupsResponse>>> GetAll(CancellationToken ct)
    {
        var groups = await groupService.GetAllAsync(ct);
        return Ok(ApiResponse<VpnServerGroupsResponse>.SuccessResponse(new VpnServerGroupsResponse { Groups = groups }));
    }

    [HttpGet("get/{id:int}")]
    public async Task<ActionResult<ApiResponse<VpnServerGroupResponse>>> GetById(int id, CancellationToken ct)
    {
        var group = await groupService.GetByIdAsync(id, ct);
        if (group is null)
            return NotFound(ApiResponse<VpnServerGroupResponse>.ErrorResponse("Group not found"));

        return Ok(ApiResponse<VpnServerGroupResponse>.SuccessResponse(new VpnServerGroupResponse { Group = group }));
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPost("create")]
    public async Task<ActionResult<ApiResponse<VpnServerGroupResponse>>> Create(
        [FromBody] CreateOrUpdateVpnServerGroupRequest request,
        CancellationToken ct)
    {
        try
        {
            var created = await groupService.CreateAsync(request.Name, ct);
            return Ok(ApiResponse<VpnServerGroupResponse>.SuccessResponse(new VpnServerGroupResponse { Group = created }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<VpnServerGroupResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPut("update/{id:int}")]
    public async Task<ActionResult<ApiResponse<VpnServerGroupResponse>>> Update(
        int id,
        [FromBody] CreateOrUpdateVpnServerGroupRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await groupService.UpdateAsync(id, request.Name, ct);
            return Ok(ApiResponse<VpnServerGroupResponse>.SuccessResponse(new VpnServerGroupResponse { Group = updated }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Group not found.")
        {
            return NotFound(ApiResponse<VpnServerGroupResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<VpnServerGroupResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpDelete("delete/{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id, CancellationToken ct)
    {
        try
        {
            await groupService.DeleteAsync(id, ct);
            return Ok(ApiResponse<bool>.SuccessResponse(true));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ApiResponse<bool>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPut("reorder")]
    public async Task<ActionResult<ApiResponse<bool>>> Reorder(
        [FromBody] ReorderVpnServerGroupsRequest request,
        CancellationToken ct)
    {
        try
        {
            await groupService.ReorderAsync(request, ct);
            return Ok(ApiResponse<bool>.SuccessResponse(true));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPut("{id:int}/set-servers")]
    public async Task<ActionResult<ApiResponse<VpnServerGroupResponse>>> SetServers(
        int id,
        [FromBody] SetVpnServerGroupServersRequest request,
        CancellationToken ct)
    {
        try
        {
            var group = await groupService.SetServersAsync(id, request, ct);
            return Ok(ApiResponse<VpnServerGroupResponse>.SuccessResponse(new VpnServerGroupResponse { Group = group }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "Group not found.")
        {
            return NotFound(ApiResponse<VpnServerGroupResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<VpnServerGroupResponse>.ErrorResponse(ex.Message));
        }
    }

    [Authorize(Roles = "Admin,App")]
    [HttpPut("ungrouped/set-servers")]
    public async Task<ActionResult<ApiResponse<bool>>> SetUngroupedServers(
        [FromBody] SetVpnServerGroupServersRequest request,
        CancellationToken ct)
    {
        try
        {
            await groupService.SetUngroupedServersAsync(request, ct);
            return Ok(ApiResponse<bool>.SuccessResponse(true));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
        }
    }
}
