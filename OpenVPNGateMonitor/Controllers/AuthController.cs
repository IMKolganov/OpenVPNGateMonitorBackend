using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Auth;
using OpenVPNGateMonitor.Services.Api.Auth;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IApplicationService _appService;

    public AuthController(IConfiguration config, IApplicationService appService)
    {
        _config = config;
        _appService = appService;
    }
    
    [HttpGet("system-secret-status")]
    public async Task<IActionResult> GetSystemStatus()
    {
        var isSet = await _appService.IsSystemApplicationSetAsync();
        return Ok(new { systemSet = isSet });
    }
    
    [HttpPost("set-system-secret")]
    public async Task<IActionResult> SetSystemSecret([FromBody] SetSecretRequest request)
    {
        var systemApp = await _appService.GetApplicationSystemByClientIdAsync(request.ClientId);

        if (systemApp != null && !string.IsNullOrEmpty(systemApp.ClientSecret))
        {
            return BadRequest(new { message = "System application is already set" });
        }

        systemApp ??= new RegisteredApp()
        {
            Name = "OpenVPN Gate Monitor Dashboard",
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            IsSystem = true
        };
        await _appService.UpdateApplicationAsync(systemApp);

        return Ok(new { message = "ClientSecret set successfully" });
    }
    
    [HttpPost("token")]
    public async Task<IActionResult> GenerateToken([FromBody] TokenRequest request)
    {
        var app = await _appService.GetApplicationByClientIdAsync(request.ClientId);
        if (app == null || app.ClientSecret != request.ClientSecret)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_config["Jwt:Secret"] 
                                          ?? throw new InvalidOperationException("Jwt:Secret"));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, request.ClientId),
                new Claim(ClaimTypes.Role, "App")
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(new { token = tokenHandler.WriteToken(token) });
    }
}