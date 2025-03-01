using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseController : ControllerBase
{
    public BaseController()
    {
    }

    [HttpGet(Name = "healthcheck")]
    public IActionResult Healthcheck()
    {
        return Ok(200);
    }
    
    [HttpGet("HealthcheckWithJwt", Name = "HealthcheckWithJwt")]
    [Authorize]
    public IActionResult HealthcheckWithJwt()
    {
        return Ok(new { status = "Healthy" });
    }
}