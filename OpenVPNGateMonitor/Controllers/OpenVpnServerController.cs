using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Controllers;

[ApiController]
[Route("[controller]")]
public class OpenVpnServerController : ControllerBase
{
    public OpenVpnServerController()
    {
    }

    [HttpGet("servers")]
    public IActionResult GetServers()
    {
        var serverInfo = new OpenVpnServerInfo
        {
            VpnMode = "server",
            Status = "running",
            Pingable = true,
            Clients = 2,
            TotalBytesIn = 5120,
            TotalBytesOut = 10240,
            UpSince = DateTime.Now.AddHours(-5).ToString("yyyy-MM-dd HH:mm:ss"),
            LocalIpAddress = "192.168.1.100"
        };

        var userStatistics = new List<OpenVpnUserSessionStatistic>
        {
            new OpenVpnUserSessionStatistic
            {
                Id = 1,
                SessionId = Guid.NewGuid(),
                CommonName = "User 1",
                RealAddress = "192.168.1.1",
                BytesReceived = 1024,
                BytesSent = 2048,
                ConnectedSince = DateTime.Now.AddHours(-1),
                LastUpdated = DateTime.Now
            },
            new OpenVpnUserSessionStatistic
            {
                Id = 2,
                SessionId = Guid.NewGuid(),
                CommonName = "User 2",
                RealAddress = "192.168.1.2",
                BytesReceived = 4096,
                BytesSent = 8192,
                ConnectedSince = DateTime.Now.AddHours(-2),
                LastUpdated = DateTime.Now
            }
        };

        var response = new OpenVpnServerFullInfo
        {
            OpenVpnServerInfo = serverInfo,
            OpenVpnUserStatistics = userStatistics
        };

        return Ok(response);
    }

}
