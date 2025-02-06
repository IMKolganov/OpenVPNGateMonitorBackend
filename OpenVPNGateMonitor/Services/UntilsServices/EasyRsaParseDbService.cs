using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Services.UntilsServices;

public class EasyRsaParseDbService : IEasyRsaParseDbService
{
    private const string Filename = "index.txt";
    private const string TempFilename = "index.txt.tmp";
    private readonly ILogger<IEasyRsaParseDbService> _logger;

    public EasyRsaParseDbService(ILogger<IEasyRsaParseDbService> logger)
    {
        _logger = logger;
    }

    public List<CertificateCaInfo> ParseCertificateInfoInIndexFile(string pkiPath)
    {
        string indexFilePath = Path.Combine(pkiPath, Filename);
        string tempFilePath = Path.Combine(pkiPath, TempFilename);

        try
        {
            File.Copy(indexFilePath, tempFilePath, true);

            var result = new List<CertificateCaInfo>();

            foreach (var line in File.ReadLines(tempFilePath))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 5)
                {
                    result.Add(new CertificateCaInfo
                    {
                        Status = ParseStatus(parts[0]),
                        ExpiryDate = ParseDate(parts[1]),
                        RevokeDate = !string.IsNullOrEmpty(parts[2]) ? ParseDate(parts[2]) : DateTime.MinValue,
                        SerialNumber = parts[3],
                        UnknownField = parts[4],
                        CommonName = parts[5].StartsWith("/CN=") ? parts[5][4..] : parts[5]
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to parse certificate in index file: {ex.Message}");
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning($"Failed to delete temporary file '{tempFilePath}': {deleteEx.Message}");
            }
        }
    }
    
    private static CertificateStatus ParseStatus(string status)
    {
        return status switch
        {
            "V" => CertificateStatus.Active,
            "R" => CertificateStatus.Revoked,
            "E" => CertificateStatus.Expired,
            _ => CertificateStatus.Unknown
        };
    }
    
    private DateTime ParseDate(string dateString)
    {
        // date format from index.txt: "250128120000Z" (YYMMDDHHMMSSZ)
        if (DateTime.TryParseExact(dateString.Substring(0, dateString.Length - 1), 
                "yyMMddHHmmss", 
                null, 
                System.Globalization.DateTimeStyles.AssumeUniversal, 
                out var date))
        {
            return date;
        }
    
        throw new FormatException($"Invalid date format: {dateString}");
    }
}