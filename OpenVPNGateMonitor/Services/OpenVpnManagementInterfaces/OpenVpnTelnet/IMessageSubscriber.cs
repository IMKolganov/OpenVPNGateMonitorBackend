namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public interface IMessageSubscriber
{
    void OnMessageReceived(string message);
}