namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public interface ICommandQueue
{
    bool HasSubscribers { get; }
    void Subscribe(IMessageSubscriber subscriber);
    Task Unsubscribe(IMessageSubscriber subscriber, string ip, int port, ICommandQueueManager queueManager);
    Task<string> SendCommandAsync(string command, int timeoutMs = 5000);
    (bool result, string? message) TryGetMessage();
    Task DisconnectAsync();
}