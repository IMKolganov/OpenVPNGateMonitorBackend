using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnManagementService : IOpenVpnManagementService
{
    private readonly OpenVpnSettings _openVpnSettings;
    private readonly DatabaseReader? _geoIpReader;

    public OpenVpnManagementService(IConfiguration configuration)
    {
        _openVpnSettings = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>() 
                           ?? throw new InvalidOperationException("OpenVpn configuration section is missing.");

        if (!string.IsNullOrEmpty(_openVpnSettings.GeoIpDatabasePath) 
            && File.Exists(_openVpnSettings.GeoIpDatabasePath))
        {
            _geoIpReader = new DatabaseReader(_openVpnSettings.GeoIpDatabasePath);
        }
    }

    private async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        client.ReceiveTimeout = 5000;
        await client.ConnectAsync(_openVpnSettings.ManagementIp, _openVpnSettings.ManagementPort, cancellationToken);

        await using var stream = client.GetStream();
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.ASCII);

        await writer.WriteLineAsync(command);

        StringBuilder response = new();
        string? line;
    
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while ((line = await reader.ReadLineAsync().WaitAsync(linkedCts.Token)) != null)
            {
                response.AppendLine(line);

                if (line.Trim() == "END") break;  
            }
        }
        catch (OperationCanceledException)
        {
            response.AppendLine("[ERROR] Timeout while reading response.");
        }

        return response.ToString();
    }


    public async Task<OpenVpnState> GetStateAsync(CancellationToken cancellationToken)
    {
        var response = await SendCommandAsync("state", cancellationToken);
        return ParseState(response);
    }

    public async Task<OpenVpnStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var response = await SendCommandAsync("load-stats", cancellationToken);
        return ParseStats(response);
    }

    public async Task<List<OpenVpnClient>> GetClientsAsync(CancellationToken cancellationToken)
    {
        var response = await SendCommandAsync("status 3", cancellationToken);
        return ParseStatus(response);
    }

    private OpenVpnState ParseState(string data)
    {
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        OpenVpnState state = new();
        
        foreach (var line in lines)
        {
            var parts = line.Split(",");
            if (parts.Length < 5) continue;
            if (!string.IsNullOrWhiteSpace(parts[0]))
            {
                if (long.TryParse(parts[0], out long timestamp))
                {
                    state.UpSince = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
                else
                {
                    throw new Exception("Invalid date.");
                }
            }
            state.Connected = parts[1] == "CONNECTED";
            state.Success = parts[2] == "SUCCESS";
            state.LocalIp = parts[3];
            state.RemoteIp = parts[4];
        }
        return state;
    }

    private OpenVpnStats ParseStats(string data)
    {
        OpenVpnStats stats = new();
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Contains("nclients="))
            {
                stats.ClientsCount = int.Parse(line.Split("nclients=")[1].Split(',')[0]);
            }
            if (line.Contains("bytesin="))
            {
                stats.BytesIn = long.Parse(line.Split("bytesin=")[1].Split(',')[0]);
            }
            if (line.Contains("bytesout="))
            {
                stats.BytesOut = long.Parse(line.Split("bytesout=")[1].Split(',')[0]);
            }
        }
        return stats;
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
                
                var geoInfo = GetGeoInfo(client.RemoteIp);//todo: add mapper for project
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
    
    private OpenVpnGeoInfo? GetGeoInfo(string ip)
    {
        if (_geoIpReader == null) return null;

        try
        {
            CityResponse cityResponse = _geoIpReader.City(ip);
            return new OpenVpnGeoInfo
            {
                Country = cityResponse.Country.IsoCode,
                Region = cityResponse.MostSpecificSubdivision.IsoCode,
                City = cityResponse.City.Name,
                Latitude = cityResponse.Location.Latitude,
                Longitude = cityResponse.Location.Longitude
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GeoIP Error: {ex.Message}");
            return null;
        }
    }
    
    private Guid GenerateSessionId(string commonName, string realAddress, DateTime connectedSince)
    {
        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
        return new Guid(hashBytes.Take(16).ToArray());
    }
}