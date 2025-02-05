using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

namespace OpenVPNGateMonitor.Services.UntilsServices;

public class EasyRsaParseDbService : IEasyRsaParseDbService
{
    private const string Filename = "index.txt";
    private readonly ILogger<IEasyRsaParseDbService> _logger;

    public EasyRsaParseDbService(ILogger<IEasyRsaParseDbService> logger)
    {
        _logger = logger;
    }

    public List<CertificateCaInfo> ParseCertificateInfoInIndexFile(string pkiPath)
    {
        try
        {
            var result = new List<CertificateCaInfo>();
            string indexFilePath = Path.Combine(pkiPath, Filename);

            foreach (var line in File.ReadLines(indexFilePath))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 5)
                {
                    result.Add(new CertificateCaInfo
                    {
                        Status = ParseStatus(parts[0]),
                        ExpiryDate = ParseDate(parts[1]),
                        RevokeDate = parts[2] != string.Empty ? ParseDate(parts[2]) : DateTime.MinValue,
                        SerialNumber = parts[3],
                        UnknownField = parts[4],
                        CommonName = parts[5].StartsWith("/CN=") ? parts[5].Substring(4) : parts[5]
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse certificate in index file: {ex.Message}");
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