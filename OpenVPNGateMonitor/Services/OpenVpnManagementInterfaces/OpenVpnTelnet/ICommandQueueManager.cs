namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public interface ICommandQueueManager
{
    Task<CommandQueue> GetOrCreateQueueAsync(string ip, int port,
        CancellationToken cancellationToken, int timeoutSeconds = 5);
    Task RemoveQueueIfNoSubscribers(string ip, int port);
}