using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnManager : IDisposable
{
    private readonly OpenVpnTelnetClient _telnetClient;
    private readonly OpenVpnCommandQueue _commandQueue;
    private readonly ILogger<OpenVpnManager> _logger;
    private readonly ConcurrentDictionary<WebSocket, byte> _webSockets = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isRunning = false;
    private bool _isProcessingQueue = false;
    private bool _isReadingTelnet = false;

    public event Action<string>? OnDisposed;

    public OpenVpnManager(string host, int port, ILoggerFactory loggerFactory, OpenVpnCommandQueue commandQueue)
    {
        _logger = loggerFactory.CreateLogger<OpenVpnManager>();
        _telnetClient = new OpenVpnTelnetClient(host, port, loggerFactory.CreateLogger<OpenVpnTelnetClient>());
        _commandQueue = commandQueue;
    }

    /// 🔹 **Гарантируем, что соединение установлено**
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_telnetClient.IsConnected)
        {
            _logger.LogInformation($"[TELNET] Connecting to { _telnetClient.Host}:{ _telnetClient.Port}");
            await _telnetClient.ConnectAsync(cancellationToken);
        }
    }

    /// 🔹 **Отправка команды через очередь**
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.EnqueueCommand(command, tcs);
        _logger.LogInformation($"[QUEUE] Command {command} added to queue. Waiting for response...");

        EnsureQueueProcessingStarted();

        return await tcs.Task.WaitAsync(cancellationToken);
    }

    /// 🔹 **Запуск обработки очереди, если не запущена**
    private void EnsureQueueProcessingStarted()
    {
        if (!_isProcessingQueue)
        {
            _isProcessingQueue = true;
            _logger.LogInformation("[QUEUE] Starting queue processing...");
            _ = Task.Run(() => ProcessCommandQueueAsync(_cts.Token));
        }
    }

    /// 🔹 **Обработка команд из очереди**
    private async Task ProcessCommandQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[QUEUE] Command queue processor started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var (command, tcs) = await _commandQueue.DequeueCommandAsync(cancellationToken);
                if (command != null)
                {
                    _logger.LogInformation($"[QUEUE] Processing command: {command}");
                    var response = await _telnetClient.SendCommandAsync(command, cancellationToken);
                    tcs.SetResult(response);

                    // 🔹 **Ответ всем WebSocket-клиентам о команде**
                    await BroadcastMessage($"[COMMAND] {command}\n{response}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[QUEUE] Command queue processing canceled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QUEUE] Error processing command.");
            }
        }

        _isProcessingQueue = false;
        _logger.LogInformation("[QUEUE] Command queue processor stopped.");
    }

    /// 🔹 **Обработка WebSocket-соединения**
    public async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        _webSockets[webSocket] = 0;

        await EnsureConnectedAsync(cancellationToken);
        _logger.LogInformation("[WS] Client connected.");

        EnsureTelnetReadingStarted();

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var buffer = new byte[1024 * 4];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var command = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation($"[WS] Received command: {command}");

                    // 🔹 **Добавляем команду в очередь (НЕ отправляем сразу в Telnet)**
                    await SendCommandAsync(command, cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS] Error in WebSocket processing.");
        }
        finally
        {
            _webSockets.TryRemove(webSocket, out _);
            await TryDisconnectAsync();
        }
    }

    /// 🔹 **Запуск чтения Telnet, если еще не запущено**
    private void EnsureTelnetReadingStarted()
    {
        if (!_isReadingTelnet)
        {
            _isReadingTelnet = true;
            _logger.LogInformation("[TELNET] Starting Telnet output processing...");
            _ = Task.Run(() => ReadTelnetOutputAsync(_cts.Token));
        }
    }

    /// 🔹 **Чтение вывода Telnet и трансляция WebSocket-клиентам**
    private async Task ReadTelnetOutputAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await _telnetClient.ReadNextLineAsync(cancellationToken);
                if (!string.IsNullOrEmpty(line))
                {
                    _logger.LogInformation($"[TELNET] {line}");

                    // 🔹 **Передаем ответ в очередь для команд**
                    await _commandQueue.CompleteCommandAsync(line);

                    // 🔹 **Отправляем всем WebSocket-клиентам весь вывод Telnet**
                    await BroadcastMessage($"[TELNET] {line}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[TELNET] Reading canceled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TELNET] Error while reading output.");
                break;
            }
        }

        _isReadingTelnet = false;
    }

    /// 🔹 **Попытка закрыть соединение, если больше нет клиентов и команд**
    private async Task TryDisconnectAsync()
    {
        if (_webSockets.IsEmpty && _commandQueue.IsEmpty)
        {
            _logger.LogInformation("[TELNET] No active connections or commands. Disconnecting.");
            _telnetClient.Dispose();
        }
    }

    /// 🔹 **Отправка сообщения всем WebSocket-клиентам**
    private async Task BroadcastMessage(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        foreach (var client in _webSockets.Keys)
        {
            if (client.State == WebSocketState.Open)
            {
                await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _telnetClient.Dispose();
    }
}
