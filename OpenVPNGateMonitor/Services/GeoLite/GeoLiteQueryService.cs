using System.Net;
using MaxMind.GeoIP2.Exceptions;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoLiteQueryService : IGeoLiteQueryService
{
    private readonly GeoLiteDatabaseFactory _dbFactory;
    private readonly ILogger<GeoLiteQueryService> _logger;

    public GeoLiteQueryService(GeoLiteDatabaseFactory dbFactory, ILogger<GeoLiteQueryService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }
    
    public async Task<string> GetDatabasePathAsync()
    {
        return await _dbFactory.GetDatabasePath();
    }
    
    public async Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var databaseReader = await _dbFactory.GetDatabaseAsync(cancellationToken);
            var metadata = databaseReader.Metadata;

            var version = metadata.BuildDate.ToString("yyyy-MM-dd HH:mm:ss");

            _logger.LogInformation("GeoLite2 database version (Build Date): {Version}", version);
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database version.");
            return "Error retrieving version.";
        }
    }
    
    public async Task<OpenVpnGeoInfo?> GetGeoInfoAsync(string ip, CancellationToken cancellationToken)
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

            var databaseReader = await _dbFactory.GetDatabaseAsync(cancellationToken);
            var cityResponse = databaseReader.City(ip);

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
