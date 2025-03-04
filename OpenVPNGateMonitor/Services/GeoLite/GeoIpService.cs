using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.Others;
using OpenVPNGateMonitor.Services.Untils;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoIpService : IGeoIpService
{
    private static readonly HttpClient Client = new HttpClient();
    private readonly ILogger<GeoIpService> _logger;
    private readonly ISettingsService _settingsService;

    public GeoIpService(ILogger<GeoIpService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("GeoLite2Updater/1.0");
    }
    
    public async Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking the latest database version...");
            
            var request = new HttpRequestMessage(HttpMethod.Head, await GetGeoIpDownloadUrl(cancellationToken));
            HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
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
    
    public async Task DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            string tempFile = "GeoLite2-City.tar.gz";
            string extractDir = "GeoLite2-City";

            _logger.LogInformation("Downloading GeoLite2 database...");

            using (var response = await Client.GetAsync(await GetGeoIpDownloadUrl(cancellationToken), HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, cancellationToken);
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
            
            File.Copy(mmdbFile, await GetGeoIpDbPath(cancellationToken), true);
            _logger.LogInformation("Database successfully updated!");

            File.Delete(tempFile);
            Directory.Delete(extractDir, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating GeoLite2 database.");
        }
    }
    
    public async Task<string> GetDataBasePath(CancellationToken cancellationToken)
    {
        return Path.GetFullPath(await GetGeoIpDbPath(cancellationToken));
    }
    
    public async Task<OpenVpnGeoInfo?> GetGeoInfo(string ip, CancellationToken cancellationToken)
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

            var databaseReader = await ReadDataBase(cancellationToken);
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

    private async Task<DatabaseReader> ReadDataBase(CancellationToken cancellationToken)
    {
        var geoIpDatabasePath = await GetGeoIpDbPath(cancellationToken);
        
        if (File.Exists(geoIpDatabasePath))
        {
           return new DatabaseReader(geoIpDatabasePath);
        }

        throw new Exception($"GeoIp database file not found at: {Path.GetFullPath(geoIpDatabasePath)}");
    }

    private async Task<string> GetGeoIpDbPath(CancellationToken cancellationToken)
    {
        // private const string DbPath = "GeoLite2-City.mmdb"; // Path to store the GeoLite2 database
        return await GetStringParamFromSettings("GeoIp_Db_Path", cancellationToken);
    }
    
    private async Task<string> GetGeoIpDownloadUrl(CancellationToken cancellationToken)
    {
        // private const string LicenseKey = "YOUR_LICENSE_KEY"; // Replace with your MaxMind License Key
        // private const string DownloadUrl = $"https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City&license_key={LicenseKey}&suffix=tar.gz";
        // var licenseKey = await GetStringParamFromSettings("LicenseKey", cancellationToken);
        return await GetStringParamFromSettings("GeoIp_Download_Url", cancellationToken);
    }


    private async Task<string> GetStringParamFromSettings(string key, CancellationToken cancellationToken)
    {
        var settingType = await _settingsService.GetValueAsync<string>($"{key}_Type", cancellationToken) ?? 
                          throw new InvalidOperationException($"Param for {key} not found.");

        if (settingType != "string")
        {
            throw new Exception($"Setting type for {key} is not string.");
        }
        
        return await _settingsService.GetValueAsync<string>(key, cancellationToken) ?? 
               throw new InvalidOperationException($"Param for {key} not found.");
    }
}
