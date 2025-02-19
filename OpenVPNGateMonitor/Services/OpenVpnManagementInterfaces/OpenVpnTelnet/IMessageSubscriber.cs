namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public interface IMessageSubscriber
{
    Task OnMessageReceived(string message, CancellationToken cancellationToken);
}