using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite.Untils;
using OpenVPNGateMonitor.Services.Others;
using OpenVPNGateMonitor.Services.Untils;

namespace OpenVPNGateMonitor.Services.GeoLite;

public class GeoLiteUpdaterService : IGeoLiteUpdaterService
{
    private readonly ILogger<GeoLiteUpdaterService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly GeoLiteDatabaseFactory _databaseFactory;
    

    public GeoLiteUpdaterService(ILogger<GeoLiteUpdaterService> logger, IServiceProvider serviceProvider,
        HttpClient httpClient, GeoLiteDatabaseFactory databaseFactory)
    {
        _httpClient = httpClient;
        _serviceProvider = serviceProvider;
        _databaseFactory = databaseFactory;
        _logger = logger;
    }
    
    public async Task<string> GetDatabaseVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking the latest database version...");
            var request = new HttpRequestMessage(HttpMethod.Head, 
                await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_Db_Path",
                    _serviceProvider, cancellationToken));
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode && response.Headers.TryGetValues("Last-Modified", out var values))
            {
                string version = values.FirstOrDefault() ?? throw new InvalidOperationException();
                _logger.LogInformation("Database version: {Version}", version);
                return version;
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
            string dbPath = await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_Db_Path", _serviceProvider,
                cancellationToken);

            _logger.LogInformation("Downloading GeoLite2 database...");
            using (var response = await _httpClient.GetAsync(await GeoLiteLoadConfigs.GetStringParamFromSettings(
                       "GeoIp_Download_Url", _serviceProvider, cancellationToken),
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
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

            _logger.LogInformation("Updating database file...");
            File.Copy(mmdbFile, dbPath, true);
            _logger.LogInformation("Database successfully updated!");

            // Notify factory to reload the database
            await _databaseFactory.ReloadDatabaseAsync(cancellationToken);

            // Cleanup temporary files
            File.Delete(tempFile);
            Directory.Delete(extractDir, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating GeoLite2 database.");
        }
    }

    // private async Task<string> GetStringParamFromSettings(string key, CancellationToken cancellationToken)
    // {
    //     using var scope = _serviceProvider.CreateScope();
    //     var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    //     
    //     var settingType = await settingsService.GetValueAsync<string>($"{key}_Type", cancellationToken) ?? 
    //                       throw new InvalidOperationException($"Param for {key} not found.");
    //
    //     if (settingType != "string")
    //     {
    //         throw new Exception($"Setting type for {key} is not string.");
    //     }
    //     
    //     return await settingsService.GetValueAsync<string>(key, cancellationToken) ?? 
    //            throw new InvalidOperationException($"Param for {key} not found.");
    // }
}
