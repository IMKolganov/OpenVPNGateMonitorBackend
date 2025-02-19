using System.Net.Sockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class TelnetClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream _stream;
    private StreamReader _reader;
    private StreamWriter _writer;
    private CancellationTokenSource _cancellationTokenSource;

    public event Action<string> OnDataReceived;

    public TelnetClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port);
        _stream = _client.GetStream();
        _reader = new StreamReader(_stream, Encoding.ASCII);
        _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

        Console.WriteLine("[TelnetClient] Connected to server.");

        _cancellationTokenSource = new CancellationTokenSource();
        //OnDataReceived += msg => Console.WriteLine($"[TelnetClient] Message received: {msg}");

        _ = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new char[1024];
            var response = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await _reader.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) continue;

                response.Append(buffer, 0, bytesRead);
                string message = response.ToString();
                
                if (message.Contains("END") || message.StartsWith("SUCCESS:", StringComparison.OrdinalIgnoreCase))
                {
                    OnDataReceived?.Invoke(message.Trim());
                    response.Clear();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Console.WriteLine($"[TelnetClient] Connection closed: {ex.Message}");
        }
    }

    public async Task SendAsync(string command)
    {
        if (_client == null || !_client.Connected)
        {
            throw new InvalidOperationException("Client is not connected");
        }
        
        await _writer.WriteLineAsync(command);
    }

    public Task DisconnectAsync()
    {
        _cancellationTokenSource.Cancel();
        _writer.Dispose();
        _reader.Dispose();
        _stream.Dispose();
        _client?.Close();
        _client = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}