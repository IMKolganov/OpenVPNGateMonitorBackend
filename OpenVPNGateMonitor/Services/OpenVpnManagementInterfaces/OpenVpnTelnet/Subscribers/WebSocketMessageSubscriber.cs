using System.Net.WebSockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet.Subscribers;

public class WebSocketMessageSubscriber : IMessageSubscriber
{
    private readonly WebSocket _webSocket;

    public WebSocketMessageSubscriber(WebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task OnMessageReceived(string message, CancellationToken cancellationToken)
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, 
                true, cancellationToken);
        }
        else
        {
            throw new WebSocketException("The websocket is not open.");
        }
    }
}
