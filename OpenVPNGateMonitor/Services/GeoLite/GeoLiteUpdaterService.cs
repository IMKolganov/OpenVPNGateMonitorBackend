using System.Text;
using System.Net.Http.Headers;
using OpenVPNGateMonitor.Models.Helpers.Api;
using OpenVPNGateMonitor.Services.GeoLite.Helpers;
using OpenVPNGateMonitor.Services.GeoLite.Interfaces;
using OpenVPNGateMonitor.Services.Helpers;

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

            var baseDir = Path.GetDirectoryName(dbPath) ?? throw new InvalidOperationException("Invalid GeoIp_Db_Path");

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
                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = HandleHttpError(response);
                    _logger.LogError("Failed to download database: {ErrorMessage}", result.ErrorMessage);
                    return result;
                }

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

            _databaseFactory.DeleteDatabase();

            File.Copy(mmdbFile, dbPath, true);

            result.DatabasePath = dbPath;
            result.Success = true;

            _logger.LogInformation("Database successfully updated.");

            await _databaseFactory.ReloadDatabaseAsync(cancellationToken);

            _logger.LogInformation("GeoLite2 database update completed successfully.");
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
    
    private string HandleHttpError(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? "Unknown";

        var errorMessage = statusCode switch
        {
            400 => "400 Bad Request – Invalid request sent to the server.",
            401 => "401 Unauthorized – Invalid API key or authentication failed.",
            403 => "403 Forbidden – Access to the resource is restricted.",
            404 => "404 Not Found – The requested resource could not be found.",
            429 => "429 Too Many Requests – Rate limit exceeded, try again later.",
            500 => "500 Internal Server Error – The server encountered an error.",
            503 => "503 Service Unavailable – The service is temporarily down, retry later.",
            _ => $"{statusCode} {reason} – Unexpected error occurred."
        };

        _logger.LogError("Failed to retrieve database version: {ErrorMessage}", errorMessage);
        return $"Error: {errorMessage}";
    }
}
