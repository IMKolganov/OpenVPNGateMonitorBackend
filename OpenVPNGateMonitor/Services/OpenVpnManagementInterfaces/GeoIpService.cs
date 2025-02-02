using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class GeoIpService : IGeoIpService
{
    private readonly DatabaseReader? _geoIpReader;

    public GeoIpService(IConfiguration configuration)
    {
        var openVpnSettings = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>() 
                              ?? throw new InvalidOperationException("OpenVpn configuration section is missing.");

        if (!string.IsNullOrEmpty(openVpnSettings.GeoIpDatabasePath) 
            && File.Exists(openVpnSettings.GeoIpDatabasePath))
        {
            _geoIpReader = new DatabaseReader(openVpnSettings.GeoIpDatabasePath);
        }
    }
    
    public OpenVpnGeoInfo? GetGeoInfo(string ip)
    {
        if (_geoIpReader == null) return null;

        try
        {
            CityResponse cityResponse = _geoIpReader.City(ip);
            return new OpenVpnGeoInfo
            {
                Country = cityResponse.Country.IsoCode,
                Region = cityResponse.MostSpecificSubdivision.IsoCode,
                City = cityResponse.City.Name,
                Latitude = cityResponse.Location.Latitude,
                Longitude = cityResponse.Location.Longitude
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GeoIP Error: {ex.Message}");
            return null;
        }
    }
}