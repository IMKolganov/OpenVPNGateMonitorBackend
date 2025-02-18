using System.Collections.Concurrent;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnCommandQueue
{
    private readonly ConcurrentQueue<(string Command, TaskCompletionSource<string> Tcs)> _queue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingCommands = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly ILogger<OpenVpnCommandQueue> _logger;

    public OpenVpnCommandQueue(ILogger<OpenVpnCommandQueue> logger)
    {
        _logger = logger;
    }

    /// ✅ **Проверка, пуста ли очередь команд**
    public bool IsEmpty => _queue.IsEmpty && _pendingCommands.IsEmpty;

    public void EnqueueCommand(string command, TaskCompletionSource<string> tcs)
    {
        _logger.LogInformation($"[QUEUE] Enqueuing command: {command}");
        _queue.Enqueue((command, tcs));
        _pendingCommands[command] = tcs; // Добавляем команду в ожидание ответа
        _queueSemaphore.Release();
        _logger.LogInformation($"[QUEUE] Command {command} successfully enqueued. Queue count: {_queue.Count}");
    }

    public async Task<(string?, TaskCompletionSource<string>)> DequeueCommandAsync(CancellationToken cancellationToken)
    {
        await _queueSemaphore.WaitAsync(cancellationToken);
        return _queue.TryDequeue(out var request) ? request : (null, new TaskCompletionSource<string>());
    }

    public async Task CompleteCommandAsync(string response)
    {
        _logger.LogInformation($"[QUEUE] Completing command with response: {response}");

        // Ищем соответствующую команду в ожидании
        foreach (var key in _pendingCommands.Keys)
        {
            if (_pendingCommands.TryRemove(key, out var tcs))
            {
                _logger.LogInformation($"[QUEUE] Command {key} completed with response: {response}");
                tcs.SetResult(response);
                return;
            }
        }

        _logger.LogWarning($"[QUEUE] No pending command found for response: {response}");
    }
}
