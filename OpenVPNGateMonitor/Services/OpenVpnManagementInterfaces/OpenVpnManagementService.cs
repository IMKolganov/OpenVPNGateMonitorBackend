using System.Net.Sockets;
using System.Text;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.Interfaces;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces;

public class OpenVpnManagementService : IOpenVpnManagementService
{
    private readonly OpenVpnSettings _openVpnSettings;
    private readonly ILogger<IOpenVpnManagementService> _logger;

    public OpenVpnManagementService(IConfiguration configuration, ILogger<IOpenVpnManagementService> logger)
    {
        _openVpnSettings = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>() 
                           ?? throw new InvalidOperationException("OpenVpn configuration section is missing.");
        _logger = logger;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to OpenVPN management interface at {Ip}:{Port}",
            _openVpnSettings.ManagementIp, _openVpnSettings.ManagementPort);

        using var client = new TcpClient();
        client.ReceiveTimeout = 5000;

        try
        {
            await client.ConnectAsync(_openVpnSettings.ManagementIp, _openVpnSettings.ManagementPort, cancellationToken);
            _logger.LogInformation("Connected successfully.");

            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream, Encoding.ASCII);
            writer.AutoFlush = true;
            using var reader = new StreamReader(stream, Encoding.ASCII);

            _logger.LogInformation("Sending command: {Command}", command);
            await writer.WriteLineAsync(command);

            StringBuilder response = new();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (await reader.ReadLineAsync().WaitAsync(linkedCts.Token) is { } line)
                {
                    response.AppendLine(line);
                    _logger.LogDebug("Received line: {Line}", line);

                    if (line.Trim() == "END")
                    {
                        _logger.LogInformation("Received END marker.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout while reading response.");
                response.AppendLine("[ERROR] Timeout while reading response.");
            }

            _logger.LogInformation("Command executed successfully.");
            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with OpenVPN management interface.");
            return $"[ERROR] {ex.Message}";
        }
    }
}
