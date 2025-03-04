using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.Untils;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoIpIpService : IGeoIpService
{
    // private const string LicenseKey = "YOUR_LICENSE_KEY"; // Replace with your MaxMind License Key
    // private const string DownloadUrl = $"https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City&license_key={LicenseKey}&suffix=tar.gz";
    // private const string DbPath = "GeoLite2-City.mmdb"; // Path to store the GeoLite2 database
    private static readonly HttpClient Client = new HttpClient();
    private readonly ILogger<GeoIpIpService> _logger;

    public GeoIpIpService(ILogger<GeoIpIpService> logger)
    {
        _logger = logger;
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("GeoLite2Updater/1.0");
    }
    
    public async Task<string> GetDatabaseVersionAsync(string downloadUrl)
    {
        try
        {
            _logger.LogInformation("Checking the latest database version...");
            
            var request = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
            HttpResponseMessage response = await Client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("Last-Modified", out var values))
                {
                    string version = values.FirstOrDefault() ?? throw new InvalidOperationException();
                    _logger.LogInformation("Database version: {Version}", version);
                    return version;
                }
            }

            _logger.LogWarning("Could not retrieve the database version.");
            return "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database version.");
            return "Error";
        }
    }
    
    public async Task DownloadAndUpdateDatabaseAsync(string downloadUrl, string dbPath)
    {
        try
        {
            string tempFile = "GeoLite2-City.tar.gz";
            string extractDir = "GeoLite2-City";

            _logger.LogInformation("Downloading GeoLite2 database...");

            using (var response = await Client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream);
            }
            
            _logger.LogInformation("Download completed. Extracting database...");

            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            Directory.CreateDirectory(extractDir);
            GZip.ExtractTarGz(tempFile, extractDir);

            var mmdbFile = Directory.GetFiles(extractDir, "*.mmdb", SearchOption.AllDirectories).FirstOrDefault();
            if (mmdbFile == null)
            {
                _logger.LogError("Database file not found after extraction.");
                return;
            }
            
            File.Copy(mmdbFile, dbPath, true);
            _logger.LogInformation("Database successfully updated!");

            File.Delete(tempFile);
            Directory.Delete(extractDir, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating GeoLite2 database.");
        }
    }
    
    public string GetDataBasePath()
    {
        return Path.GetFullPath(GetGeoIpDbPath());
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

            var geoIpSettings = new GeoIpSettings();
            CityResponse cityResponse = ReadDataBase(geoIpSettings)!.City(ip);

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

    private DatabaseReader ReadDataBase(GeoIpSettings geoIpSettings)
    {
        if (File.Exists(geoIpSettings.GeoIpDatabasePath))
        {
           return new DatabaseReader(geoIpSettings.GeoIpDatabasePath);
        }

        throw new Exception($"GeoIp database file not found at: {Path.GetFullPath(geoIpSettings.GeoIpDatabasePath)}");
    }

    private string GetGeoIpDbPath()
    {
        throw new NotImplementedException();
        // return ""
    }
    
}
