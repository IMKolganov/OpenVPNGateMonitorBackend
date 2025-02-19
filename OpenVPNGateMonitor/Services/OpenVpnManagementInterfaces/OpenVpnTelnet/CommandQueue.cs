using System.Collections.Concurrent;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class CommandQueue
{
    private readonly ConcurrentQueue<string> _messageQueue = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _pendingCommands = new();
    private readonly TelnetClient _telnetClient;
    private readonly List<IMessageSubscriber> _subscribers = new();
    
    public bool HasSubscribers => _subscribers.Count > 0;
    
    public CommandQueue(TelnetClient telnetClient)
    {
        _telnetClient = telnetClient;
        _telnetClient.OnDataReceived += HandleIncomingMessage;
    }

    public void Subscribe(IMessageSubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }
    
    public async Task Unsubscribe(IMessageSubscriber subscriber, string ip, int port, ICommandQueueManager queueManager)
    {
        if (!_subscribers.Remove(subscriber))
        {
            throw new Exception("Subscriber doesn't exist");
        }
        if (_subscribers.Count == 0)
        {
            await queueManager.RemoveQueueIfNoSubscribers(ip, port);
        }
    }

    private void HandleIncomingMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        Console.WriteLine($"[CommandQueue] Received message: {message}");

        if (message.Contains("END") || message.StartsWith("SUCCESS:", StringComparison.OrdinalIgnoreCase))
        {
            if (_pendingCommands.TryRemove(_pendingCommands.Keys.FirstOrDefault(), out var tcs))
            {
                tcs.TrySetResult(message.Trim());
            }
            else
            {
                Console.WriteLine("[CommandQueue] No pending command found, adding to queue.");
                _messageQueue.Enqueue(message.Trim());
                NotifySubscribers(message);
            }
        }
        else
        {
            Console.WriteLine("[CommandQueue] Message not complete, adding to unprocessed queue.");
            _messageQueue.Enqueue(message.Trim());
            NotifySubscribers(message);
        }
    }

    private void NotifySubscribers(string message)
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.OnMessageReceived(message);
        }
    }

    public async Task<string> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        var cmdId = Guid.NewGuid();
        var tcs = new TaskCompletionSource<string>();
        _pendingCommands[cmdId] = tcs;

        await _telnetClient.SendAsync(command);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

        if (completedTask == tcs.Task)
        {
            _pendingCommands.TryRemove(cmdId, out _);
            var response = await tcs.Task;
            return response;
        }
        else
        {
            _pendingCommands.TryRemove(cmdId, out _);
            throw new TimeoutException($"[ERROR] Command \"{command}\" timed out after {timeoutMs}ms.");
        }
    }

    public (bool result, string? message) TryGetMessage()
    {
        var result = _messageQueue.TryDequeue(out var message);
        return (result, message);
    }
    
    public async Task DisconnectAsync()
    {
        await _telnetClient.DisconnectAsync();
    }
}