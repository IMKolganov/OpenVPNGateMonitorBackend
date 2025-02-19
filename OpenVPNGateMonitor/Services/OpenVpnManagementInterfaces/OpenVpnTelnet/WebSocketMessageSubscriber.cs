using System.Net.WebSockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class WebSocketMessageSubscriber : IMessageSubscriber
{
    private readonly WebSocket _webSocket;

    public WebSocketMessageSubscriber(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async void OnMessageReceived(string message)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
