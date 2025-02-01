using Microsoft.AspNetCore.Mvc;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("[controller]")]
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
}