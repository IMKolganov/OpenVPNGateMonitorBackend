using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Others;
using OpenVPNGateMonitor.SharedModels.Responses;
using OpenVPNGateMonitor.SharedModels.Settings.Requests;
using OpenVPNGateMonitor.SharedModels.Settings.Responses;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet("Get")]
    public async Task<IActionResult> Get([FromQuery] GetSettingRequest request, CancellationToken cancellationToken = default)
    {
        var settingType = await settingsService.GetValueAsync<string>($"{request.Key}_Type", cancellationToken);
        if (settingType == null)
        {
            return NotFound(ApiResponse<string>.ErrorResponse($"Setting '{request.Key}' not found."));
        }

        object? value = settingType.ToLower() switch
        {
            "int" => await settingsService.GetValueAsync<int>(request.Key, cancellationToken),
            "bool" => await settingsService.GetValueAsync<bool>(request.Key, cancellationToken),
            "double" => await settingsService.GetValueAsync<double>(request.Key, cancellationToken),
            "datetime" => await settingsService.GetValueAsync<DateTime>(request.Key, cancellationToken),
            "string" => await settingsService.GetValueAsync<string>(request.Key, cancellationToken),
            _ => null
        };

        if (value is null)
        {
            return NotFound(ApiResponse<string>.ErrorResponse($"Setting '{request.Key}' not found."));
        }

        return Ok(ApiResponse<SettingResponse>.SuccessResponse(new SettingResponse { Key = request.Key, Value = value }));
    }

    [HttpPost("Set")]
    public async Task<IActionResult> Set(
        [FromBody] SetSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        object? convertedValue = request.Type.ToLower() switch
        {
            "int" => int.TryParse(request.Value, out var intValue) ? intValue : null,
            "bool" => bool.TryParse(request.Value, out var boolValue) ? boolValue : null,
            "double" => double.TryParse(request.Value, out var doubleValue) ? doubleValue : null,
            "datetime" => DateTime.TryParse(request.Value, out var dateTimeValue) ? dateTimeValue : null,
            "string" => request.Value,
            _ => null
        };

        if (convertedValue is null)
        {
            return BadRequest(ApiResponse<string>.ErrorResponse($"Invalid value '{request.Value}' for type '{request.Type}'."));
        }

        await settingsService.SetValueAsync(request.Key, convertedValue, cancellationToken);
        await settingsService.SetValueAsync($"{request.Key}_Type", request.Type.ToLower(), cancellationToken);

        return Ok(ApiResponse<SettingResponse>.SuccessResponse(new SettingResponse { Key = request.Key, Value = convertedValue }));
    }
}