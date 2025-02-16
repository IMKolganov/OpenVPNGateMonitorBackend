using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class GeoIpService : IGeoIpService
{
    private readonly ILogger<IGeoIpService> _logger;
    private readonly DatabaseReader? _geoIpReader;
    private readonly string _geoIpDbPath;

    public GeoIpService(ILogger<IGeoIpService> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var openVpnSettings = configuration.GetSection("GeoIp").Get<GeoIpSettings>() 
                              ?? throw new InvalidOperationException("GeoIp configuration section is missing.");
        _geoIpDbPath = openVpnSettings.GeoIpDatabasePath;
        if (!string.IsNullOrEmpty(openVpnSettings.GeoIpDatabasePath))
        {
            _logger.LogInformation($"Checking GeoIP database at: {Path.GetFullPath(openVpnSettings.GeoIpDatabasePath)}");

            if (File.Exists(openVpnSettings.GeoIpDatabasePath))
            {
                _geoIpReader = new DatabaseReader(openVpnSettings.GeoIpDatabasePath);
                _logger.LogInformation("GeoIP database successfully loaded.");
            }
            else
            {
                _logger.LogError($"GeoIp database file not found at: {Path.GetFullPath(openVpnSettings.GeoIpDatabasePath)}");
            }
        }
        else
        {
            _logger.LogError("GeoIp database configuration section is missing or empty.");
        }
    }

    public string GetDataBasePath()
    {
        return Path.GetFullPath(_geoIpDbPath);
    }
    
    public OpenVpnGeoInfo? GetGeoInfo(string ip)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ip))
                return null;

            var ipAddress = IPAddress.Parse(ip);
            
            if (IsPrivateIp(ipAddress))
            {
                return new OpenVpnGeoInfo
                {
                    Country = "Internet",
                    Region = "RFC1918",
                    City = "RFC1918",
                    Latitude = 0,
                    Longitude = 0
                };
            }

            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6Multicast)
                return null;

            CityResponse cityResponse = _geoIpReader!.City(ip);

            return new OpenVpnGeoInfo
            {
                Country = cityResponse.RegisteredCountry?.IsoCode ?? cityResponse.Country?.IsoCode,
                Region = cityResponse.MostSpecificSubdivision?.IsoCode 
                         ?? cityResponse.Subdivisions.LastOrDefault()?.IsoCode
                         ?? cityResponse.RegisteredCountry?.IsoCode,
                City = cityResponse.City?.Name 
                       ?? cityResponse.Location?.TimeZone 
                       ?? cityResponse.RegisteredCountry?.Name,
                Latitude = cityResponse.Location?.Latitude,
                Longitude = cityResponse.Location?.Longitude
            };
        }
        catch (AddressNotFoundException ex)
        {
            _logger.LogWarning($"GeoIP not found for {ip}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting GeoIP for {ip} Error: {ex.Message}");
            return null;
        }
    }

    private bool IsPrivateIp(IPAddress ip)
    {
        byte[] bytes = ip.GetAddressBytes();
        return (bytes[0] == 10) ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               IPAddress.IsLoopback(ip);
    }
}