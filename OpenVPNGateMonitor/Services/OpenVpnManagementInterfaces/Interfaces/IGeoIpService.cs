using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

public interface IGeoIpService
{
    OpenVpnGeoInfo? GetGeoInfo(string ip);
    string GetDataBasePath();
}