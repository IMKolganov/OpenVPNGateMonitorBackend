using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnClientService : IOpenVpnClientService
{
    private readonly ILogger<IOpenVpnClientService> _logger;
    private readonly IGeoIpService _geoIpService;
    private readonly OpenVpnManagerPool _openVpnManagerPool;
    
    public OpenVpnClientService(ILogger<IOpenVpnClientService> logger, IGeoIpService geoIpService, 
        OpenVpnManagerPool openVpnManagerPool)
    {
        _logger = logger;
        _geoIpService = geoIpService; 
        _openVpnManagerPool = openVpnManagerPool;
    }
    
    public async Task<List<OpenVpnClient>> GetClientsAsync(string managementIp, int managementPort, 
        CancellationToken cancellationToken)
    {
        var manager = _openVpnManagerPool.GetOrCreateManager(managementIp, managementPort);
        var response = await manager.SendCommandAsync("status 3", cancellationToken);
        return ParseStatus(response);
    }

    private List<OpenVpnClient> ParseStatus(string data)
    {
        var id = 0;
        var clients = new List<OpenVpnClient>();
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("CLIENT_LIST"))
            {
                var parts = line.Split("\t");
                if (parts.Length < 8) continue;

                var client = TryParseClient(parts);
                
                if (client != null)
                {
                    client.Id = id;//todo: remove
                    id++;
                    var geoInfo = _geoIpService.GetGeoInfo(client.RemoteIp);//todo: add mapper for project
                    if (geoInfo != null)
                    {
                        client.Country = geoInfo.Country;
                        client.Region = geoInfo.Region;
                        client.City = geoInfo.City;
                        client.Latitude = geoInfo.Latitude;
                        client.Longitude = geoInfo.Longitude;
                    }
                

                    var sessionId = GenerateSessionId(client.CommonName, 
                        client.RemoteIp, client.ConnectedSince);
                    
                    client.SessionId = sessionId;
                    //save to db
                    clients.Add(client);
                }
            }
        }
        return clients;
    }

    private OpenVpnClient? TryParseClient(string[] parts)
    {
        try
        {
            return new OpenVpnClient()
            {
                CommonName = parts[1],
                RemoteIp = parts[2].Split(":")[0],
                LocalIp = parts[3],
                BytesReceived = TryParseLong(parts[5], "BytesReceived"),
                BytesSent = TryParseLong(parts[6], "BytesSent"),
                ConnectedSince = TryParseDateTime(parts[7], "ConnectedSince"),
                Username = parts[9] == "UNDEF" ? parts[1] : parts[9]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing OpenVPN client data. Raw parts: {Parts}", string.Join("|", parts));
            return null;
        }
    }
    
    private long TryParseLong(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("{FieldName} is empty. Using default value 0.", fieldName);
            return 0;
        }

        if (long.TryParse(value, out var result))
            return result;

        _logger.LogError("Failed to parse {FieldName}. Value: '{Value}'", fieldName, value);
        throw new FormatException($"Invalid long format in field {fieldName}: '{value}'");
    }
    
    private DateTimeOffset TryParseDateTimeOffset(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("{FieldName} is empty. Using default DateTimeOffset.MinValue.", fieldName);
            return DateTimeOffset.MinValue;
        }

        if (long.TryParse(value, out var unixTime))
        {
            try
            {
                var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                _logger.LogInformation("Parsed {FieldName} as Unix Timestamp: {DateTime}", fieldName, dateTimeOffset);
                return dateTimeOffset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse {FieldName} as Unix Timestamp. Value: '{Value}'", fieldName, value);
            }
        }

        _logger.LogError("Failed to parse {FieldName}. Value: '{Value}'", fieldName, value);
        throw new FormatException($"Invalid DateTimeOffset format in field {fieldName}: '{value}'");
    }


    private DateTime TryParseDateTime(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("{FieldName} is empty. Using default DateTime.MinValue.", fieldName);
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        if (long.TryParse(value, out var unixTime))
        {
            try
            {
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                _logger.LogInformation("Parsed {FieldName} as Unix Timestamp: {DateTime}", fieldName, dateTime);
                return dateTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse {FieldName} as Unix Timestamp. Value: '{Value}'", fieldName, value);
            }
        }

        _logger.LogError("Failed to parse {FieldName}. Value: '{Value}'", fieldName, value);
        throw new FormatException($"Invalid DateTime format in field {fieldName}: '{value}'");
    }
    
    private Guid GenerateSessionId(string commonName, string realAddress, DateTimeOffset connectedSince)
    {
        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
        return new Guid(hashBytes.Take(16).ToArray());
    }
}