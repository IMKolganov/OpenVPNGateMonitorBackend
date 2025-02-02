using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnClientService
{
    private readonly ILogger<OpenVpnClientService> _logger;
    private readonly IGeoIpService _geoIpService;
    private readonly IOpenVpnManagementService _openVpnManagementService;
    
    public OpenVpnClientService(ILogger<OpenVpnClientService> logger, IGeoIpService geoIpService, 
        OpenVpnManagementService openVpnManagementService)
    {
        _logger = logger;
        _geoIpService = geoIpService;
        _openVpnManagementService = openVpnManagementService;
    }
    
    public async Task<List<OpenVpnClient>> GetClientsAsync(CancellationToken cancellationToken)
    {
        var response = await _openVpnManagementService.SendCommandAsync("status 3", cancellationToken);
        return ParseStatus(response);
    }
    

    private List<OpenVpnClient> ParseStatus(string data)
    {
        var clients = new List<OpenVpnClient>();
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("CLIENT_LIST"))
            {
                var parts = line.Split("\t");
                if (parts.Length < 8) continue;

                var client = new OpenVpnClient()
                {
                    CommonName = parts[1],
                    RemoteIp = parts[2].Split(":")[0],
                    LocalIp = parts[3],
                    BytesReceived = long.Parse(parts[4]),
                    BytesSent = long.Parse(parts[5]),
                    ConnectedSince = DateTime.Parse(parts[6], CultureInfo.InvariantCulture),
                    Username = parts[7] == "UNDEF" ? parts[1] : parts[7]
                };
                
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
        return clients;
    }
    
    private Guid GenerateSessionId(string commonName, string realAddress, DateTime connectedSince)
    {
        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
        return new Guid(hashBytes.Take(16).ToArray());
    }
}