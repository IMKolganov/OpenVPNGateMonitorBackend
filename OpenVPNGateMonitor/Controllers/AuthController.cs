using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Auth;
using OpenVPNGateMonitor.Services.Api.Auth;
using OpenVPNGateMonitor.SharedModels.Auth.Responses;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IConfiguration config, IApplicationService appService) : ControllerBase
{
    [HttpGet("system-secret-status")]
    public async Task<IActionResult> GetSystemStatus()
    {
        var isSet = await appService.IsSystemApplicationSetAsync();
        return Ok(new SystemSecretStatusResponse { SystemSet = isSet });
    }

    [HttpPost("set-system-secret")]
    public async Task<IActionResult> SetSystemSecret([FromBody] SetSecretRequest request)
    {
        var systemApp = await appService.GetApplicationSystemByClientIdAsync(request.ClientId);

        if (systemApp is { ClientSecret: not null })
        {
            return BadRequest(new AuthResponse { Message = "System application is already set" });
        }

        var hashedSecret = BCrypt.Net.BCrypt.HashPassword(request.ClientSecret);

        systemApp ??= new ClientApplication
        {
            Name = "OpenVPN Gate Monitor Dashboard",
            ClientId = request.ClientId,
            ClientSecret = hashedSecret,
            IsSystem = true
        };

        await appService.UpdateApplicationAsync(systemApp);

        return Ok(new AuthResponse { Message = "ClientSecret set successfully" });
    }

    [HttpPost("token")]
    public async Task<IActionResult> GenerateToken([FromBody] TokenRequest request)
    {
        var app = await appService.GetApplicationByClientIdAsync(request.ClientId);
        if (app == null)
        {
            return Unauthorized(new AuthResponse { Message = "Invalid credentials" });
        }

        var isValid = app.IsSystem
            ? BCrypt.Net.BCrypt.Verify(request.ClientSecret, app.ClientSecret)
            : app.ClientSecret == request.ClientSecret;

        if (!isValid)
        {
            return Unauthorized(new AuthResponse { Message = "Invalid credentials" });
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(config["Jwt:Secret"]
                                          ?? throw new InvalidOperationException("Jwt:Secret"));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, request.ClientId),
                new Claim(ClaimTypes.Role, "App")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(new TokenResponse
        {
            Token = tokenHandler.WriteToken(token),
            Expiration = tokenDescriptor.Expires ?? DateTime.UtcNow
        });
    }
}
