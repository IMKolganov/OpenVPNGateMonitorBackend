namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public interface ICommandQueueManager
{
    Task<CommandQueue> GetOrCreateQueueAsync(string ip, int port);
    Task RemoveQueueIfNoSubscribers(string ip, int port);
}