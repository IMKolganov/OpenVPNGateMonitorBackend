using System.Globalization;
using System.Net.Sockets;
using System.Text;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;
using OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices;

public class VpnManagementService : IVpnManagementService
{
    private readonly OpenVpnSettings _openVpnSettings;
    private readonly DatabaseReader? _geoIpReader;

    public VpnManagementService(IConfiguration configuration)
    {
        _openVpnSettings = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>() 
                           ?? throw new InvalidOperationException("OpenVpn configuration section is missing.");

        if (!string.IsNullOrEmpty(_openVpnSettings.GeoIpDatabasePath) 
            && File.Exists(_openVpnSettings.GeoIpDatabasePath))
        {
            _geoIpReader = new DatabaseReader(_openVpnSettings.GeoIpDatabasePath);
        }
    }

    private async Task<string> SendCommandAsync(string command)
    {
        using var client = new TcpClient
        {
            ReceiveTimeout = 5000 // 5 seconds timeout for response
        };
        await client.ConnectAsync(_openVpnSettings.ManagementIp, _openVpnSettings.ManagementPort);

        await using var stream = client.GetStream();
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.ASCII);

        await writer.WriteLineAsync(command);

        StringBuilder response = new();
        string? line;

        // Читаем ответ построчно до появления пустой строки
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) break; // Выход по пустой строке (конец ответа OpenVPN)
            response.AppendLine(line);
        }

        return response.ToString();
    }

    public async Task<string> GetVersionAsync()
    {
        return await SendCommandAsync("version");
    }

    public async Task<OpenVpnState> GetStateAsync()
    {
        var response = await SendCommandAsync("state");
        return ParseState(response);
    }

    public async Task<OpenVpnStats> GetStatsAsync()
    {
        var response = await SendCommandAsync("load-stats");
        return ParseStats(response);
    }

    public async Task<List<OpenVpnClient>> GetClientsAsync()
    {
        var response = await SendCommandAsync("status 3");
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
                stats.ClientsCount = int.Parse(line.Split("nclients=")[1]);
            }
            if (line.Contains("bytesin="))
            {
                stats.BytesIn = long.Parse(line.Split("bytesin=")[1]);
            }
            if (line.Contains("bytesout="))
            {
                stats.BytesOut = long.Parse(line.Split("bytesout=")[1]);
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
                
                var geoInfo = GetGeoInfo(client.RemoteIp);
                if (geoInfo != null)
                {
                    client.Country = geoInfo.Country;
                    client.Region = geoInfo.Region;
                    client.City = geoInfo.City;
                    client.Latitude = geoInfo.Latitude;
                    client.Longitude = geoInfo.Longitude;
                }

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
}