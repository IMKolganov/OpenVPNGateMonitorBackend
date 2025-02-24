using System.Collections.Concurrent;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class CommandQueueManager : ICommandQueueManager
{
    private readonly ConcurrentDictionary<string, CommandQueue> _queues = new();

    public async Task<CommandQueue> GetOrCreateQueueAsync(string ip, int port,
        CancellationToken cancellationToken, int timeoutSeconds = 5)
    {
        string key = $"{ip}:{port}";

        if (_queues.TryGetValue(key, out var existingQueue))
        {
            return existingQueue;
        }

        var newClient = new TelnetClient(ip, port);
        await newClient.ConnectAsync(cancellationToken, timeoutSeconds);

        var newQueue = new CommandQueue(newClient);
        _queues[key] = newQueue;

        return newQueue;
    }
    
    public async Task RemoveQueueIfNoSubscribers(string ip, int port)
    {
        string key = $"{ip}:{port}";

        if (_queues.TryGetValue(key, out var queue) && !queue.HasSubscribers)
        {
            _queues.TryRemove(key, out _);
            await queue.DisconnectAsync();
            Console.WriteLine($"[CommandQueueManager] Removed queue for {ip}:{port} due to no subscribers.");
        }
    }
}
