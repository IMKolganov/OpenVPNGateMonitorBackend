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
            ManagementIp = "openvpn_udp",
            ManagementPort = 5092
        },
        new OpenVpnServer
        {
            Id = 2,
            ServerName = "OpenVPN Server (tcp)",
            ManagementIp = "openvpn_tcp",
            ManagementPort = 5093
        },
    };
}