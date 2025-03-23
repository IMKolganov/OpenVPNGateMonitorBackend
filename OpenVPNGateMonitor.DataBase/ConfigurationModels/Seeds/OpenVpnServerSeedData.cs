using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class OpenVpnServerSeedData
{ 
    public static readonly OpenVpnServer[] Data =
    {
        new OpenVpnServer
        {
            Id = 1, 
            ServerName = "OpenVPN Server (udp)",
            ManagementIp = "127.0.0.1",
            ManagementPort = 5093
        },
        new OpenVpnServer
        {
            Id = 2,
            ServerName = "OpenVPN Server (tcp)",
            ManagementIp = "127.0.0.1",
            ManagementPort = 5092
        },
    };
}