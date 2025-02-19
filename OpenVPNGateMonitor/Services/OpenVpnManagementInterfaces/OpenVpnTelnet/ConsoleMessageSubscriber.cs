namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class ConsoleMessageSubscriber : IMessageSubscriber
{
    public ConsoleMessageSubscriber()
    {
        
    }
    
    public void OnMessageReceived(string message)
    {
        Console.WriteLine($"[SUBSCRIBER] Received: {message}");
    }
}