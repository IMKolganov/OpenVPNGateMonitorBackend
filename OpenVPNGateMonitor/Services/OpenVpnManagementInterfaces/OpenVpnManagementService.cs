using System.Net.Sockets;
using System.Text;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnManagementService : IOpenVpnManagementService
{
    private readonly OpenVpnSettings _openVpnSettings;

    public OpenVpnManagementService(IConfiguration configuration)
    {
        _openVpnSettings = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>() 
                           ?? throw new InvalidOperationException("OpenVpn configuration section is missing.");
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        client.ReceiveTimeout = 5000;
        await client.ConnectAsync(_openVpnSettings.ManagementIp, _openVpnSettings.ManagementPort, cancellationToken);

        await using var stream = client.GetStream();
        await using var writer = new StreamWriter(stream, Encoding.ASCII);
        writer.AutoFlush = true;
        using var reader = new StreamReader(stream, Encoding.ASCII);

        await writer.WriteLineAsync(command);

        StringBuilder response = new();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (await reader.ReadLineAsync().WaitAsync(linkedCts.Token) is { } line)
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


}