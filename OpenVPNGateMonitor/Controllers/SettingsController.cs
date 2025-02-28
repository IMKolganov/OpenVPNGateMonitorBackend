using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.Others;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(IOpenVpnTelnetService openVpnTelnetService, ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet("Get")]
    public async Task<IActionResult> Get([FromQuery] string key, CancellationToken cancellationToken = default)
    {
        var settingType = await _settingsService.GetValueAsync<string>($"{key}_Type", cancellationToken);
        if (settingType == null)
        {
            return NotFound($"Setting '{key}' not found.");
        }

        object? value = settingType switch
        {
            "int" => await _settingsService.GetValueAsync<int>(key, cancellationToken),
            "bool" => await _settingsService.GetValueAsync<bool>(key, cancellationToken),
            "double" => await _settingsService.GetValueAsync<double>(key, cancellationToken),
            "datetime" => await _settingsService.GetValueAsync<DateTime>(key, cancellationToken),
            "string" => await _settingsService.GetValueAsync<string>(key, cancellationToken),
            _ => null
        };

        return value is null ? NotFound($"Setting '{key}' not found.") : Ok(new { key, value });
    }

    [HttpPost("Set")]
    public async Task<IActionResult> Set(
        [FromQuery] string key, 
        [FromQuery] string value, 
        [FromQuery] string type, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(type))
        {
            return BadRequest("Key, value, and type are required.");
        }

        object? convertedValue = type.ToLower() switch
        {
            "int" => int.TryParse(value, out var intValue) ? intValue : null,
            "bool" => bool.TryParse(value, out var boolValue) ? boolValue : null,
            "double" => double.TryParse(value, out var doubleValue) ? doubleValue : null,
            "datetime" => DateTime.TryParse(value, out var dateTimeValue) ? dateTimeValue : null,
            "string" => value,
            _ => null
        };

        if (convertedValue is null)
        {
            return BadRequest($"Invalid value '{value}' for type '{type}'. Supported types: int, bool, double, datetime, string.");
        }

        await _settingsService.SetValueAsync(key, convertedValue, cancellationToken);
        await _settingsService.SetValueAsync($"{key}_Type", type.ToLower(), cancellationToken);

        return Ok(new { key, value = convertedValue });
    }
}