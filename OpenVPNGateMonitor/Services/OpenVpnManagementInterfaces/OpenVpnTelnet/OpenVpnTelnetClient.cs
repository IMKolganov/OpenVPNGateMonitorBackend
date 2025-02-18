using System.Net.Sockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnTelnetClient : IDisposable
{
    private readonly ILogger<OpenVpnTelnetClient> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TcpClient? _client;
    private StreamWriter? _writer;
    private StreamReader? _reader;

    public string Host { get; }
    public int Port { get; }

    private bool _isConnected;
    public bool IsConnected => _isConnected && _client is { Connected: true };

    public OpenVpnTelnetClient(string host, int port, ILogger<OpenVpnTelnetClient> logger)
    {
        Host = host;
        Port = port;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return true;

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(Host, Port, cancellationToken);
            var stream = _client.GetStream();
            _writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
            _reader = new StreamReader(stream, Encoding.ASCII);
            _isConnected = true;
            _logger.LogInformation($"[TELNET] Connected to OpenVPN {Host}:{Port}.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TELNET] Connection failed.");
            Dispose();
            return false;
        }
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (!IsConnected || _writer == null || _reader == null)
        {
            _logger.LogError($"[TELNET] Attempted to send command while disconnected: {command}");
            return "[ERROR] Not connected.";
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation($"[TELNET] Sending command: {command}");
            await _writer.WriteLineAsync(command);
            await _writer.FlushAsync();

            _logger.LogInformation($"[TELNET] Waiting for response for command: {command}");

            var response = new StringBuilder();
            string? line;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while ((line = await _reader.ReadLineAsync(linkedCts.Token)) != null)
                {
                    _logger.LogInformation($"[TELNET] Received: {line}");
                    response.AppendLine(line);

                    // ✅ Останавливаемся на "END" для многострочных команд
                    if (line.Trim() == "END") break;

                    // ✅ Завершаем на "SUCCESS:" или "ERROR:"
                    if (line.StartsWith("SUCCESS:") || line.StartsWith("ERROR:")) break;

                    // ❌ Предохраняемся от зависания в `bytecount`
                    if (command.StartsWith("bytecount", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"[TELNET] Bytecount command detected: {command}. Ignoring further output.");
                        break;
                    }

                    // ✅ Для однострочных команд читаем только первую строку
                    if (command == "version" || command == "pid" ||
                        command.StartsWith("kill ", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError($"[TELNET] Timeout or cancellation while executing command: {command}");
                return "[ERROR] Timeout waiting for response.";
            }

            return response.ToString().Trim();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<string?> ReadNextLineAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected || _reader == null)
        {
            _logger.LogError("[TELNET] Attempted to read while disconnected.");
            return null;
        }

        try
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line != null)
            {
                _logger.LogInformation($"[TELNET] Read line: {line}");
            }
            return line;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[TELNET] Read operation was canceled.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TELNET] Error while reading line.");
            return null;
        }
    }

    public void Dispose()
    {
        _isConnected = false;
        _client?.Dispose();
        _writer?.Dispose();
        _reader?.Dispose();
    }
}
