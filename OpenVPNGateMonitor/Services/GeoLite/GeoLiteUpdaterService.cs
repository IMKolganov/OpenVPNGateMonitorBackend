using System.Text;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.GeoLite.Untils;
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

            var downloadUrl = await GetDownloadUrlAsync(cancellationToken);
            var credentials = await GetAuthHeaderAsync(cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.TryGetValues("Last-Modified", out var lastModifiedValues))
            {
                var lastModified = lastModifiedValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(lastModified))
                {
                    _logger.LogInformation("GeoLite2 Last-Modified: {LastModified}", lastModified);
                    return lastModified;
                }
            }

            if (response.Content.Headers.TryGetValues("Content-Disposition", out var contentDispositionValues))
            {
                var contentDisposition = contentDispositionValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(contentDisposition))
                {
                    var match = Regex.Match(contentDisposition, @"\d{8}");
                    if (match.Success)
                    {
                        var version = match.Value;
                        _logger.LogInformation("GeoLite2 version from Content-Disposition: {Version}", version);
                        return version;
                    }
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

    public async Task<GeoLiteUpdateResponse> DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken)
    {
        var result = new GeoLiteUpdateResponse();

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            var dbPath =
                await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_Db_Path", _serviceProvider,
                    cancellationToken);
            if (string.IsNullOrEmpty(dbPath))
                throw new InvalidOperationException("GeoIp_Db_Path is not configured.");

            var baseDir = Path.GetDirectoryName(dbPath) ??
                          throw new InvalidOperationException("Invalid GeoIp_Db_Path");

            var extractDir = Path.Combine(baseDir, $"GeoLite2_{timestamp}");
            var tempFile = Path.Combine(extractDir, $"GeoLite2-City_{timestamp}.tar.gz");

            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);
            if (!Directory.Exists(extractDir))
                Directory.CreateDirectory(extractDir);

            var downloadUrl = await GetDownloadUrlAsync(cancellationToken);
            var credentials = await GetAuthHeaderAsync(cancellationToken);

            result.DownloadUrl = downloadUrl;
            result.TempFilePath = tempFile;

            _logger.LogInformation("Downloading GeoLite2 database...");

            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using (var response =
                   await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream =
                    new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            _logger.LogInformation("Download completed. Extracting database...");

            GZip.ExtractTarGz(tempFile, extractDir);

            var extractedDirs = Directory.GetDirectories(extractDir);
            if (extractedDirs.Length == 0)
            {
                result.ErrorMessage = "Extraction failed: No directories found.";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            var extractedPath = extractedDirs.First();
            result.ExtractedPath = extractedPath;

            _logger.LogInformation("Extracted database directory: {ExtractedPath}", extractedPath);

            var mmdbFile = Directory.GetFiles(extractedPath, "*.mmdb", SearchOption.AllDirectories).FirstOrDefault();
            if (mmdbFile == null)
            {
                result.ErrorMessage = "Database file not found after extraction.";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            _logger.LogInformation("Updating database file...");

            await _databaseFactory.CloseDatabaseAsync(cancellationToken);

            if (File.Exists(dbPath))
            {
                _logger.LogInformation("Deleting old database file: {DbPath}", dbPath);
                
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.Delete(dbPath);
                        break;
                    }
                    catch (IOException)
                    {
                        _logger.LogWarning("File is still locked. Retrying in 500ms...");
                        await Task.Delay(500, cancellationToken);
                    }
                }
            }

            File.Copy(mmdbFile, dbPath, true);

            result.DatabasePath = dbPath;
            result.Success = true;
            result.Version = await GetDatabaseVersionAsync(cancellationToken);

            _logger.LogInformation("Database successfully updated to version: {Version}", result.Version);

            await _databaseFactory.ReloadDatabaseAsync(cancellationToken);

            // File.Delete(tempFile);
            // Directory.Delete(extractedPath, true);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error updating GeoLite2 database.");
        }

        return result;
    }

    private async Task<string> GetDownloadUrlAsync(CancellationToken cancellationToken)
    {
        return await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_Download_Url", _serviceProvider, cancellationToken)
            ?? throw new InvalidOperationException("GeoIp_Download_Url is not configured.");
    }

    private async Task<string> GetAuthHeaderAsync(CancellationToken cancellationToken)
    {
        var accountId = await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_Account_ID", _serviceProvider, cancellationToken);
        var licenseKey = await GeoLiteLoadConfigs.GetStringParamFromSettings("GeoIp_License_Key", _serviceProvider, cancellationToken);

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(licenseKey))
            throw new InvalidOperationException("GeoLite Account ID or License Key is missing.");

        return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountId}:{licenseKey}"));
    }
}
