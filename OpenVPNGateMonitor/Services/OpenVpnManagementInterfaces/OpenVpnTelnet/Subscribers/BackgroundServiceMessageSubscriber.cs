using System.Net.WebSockets;
using System.Text;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet.Subscribers;

public class BackgroundServiceMessageSubscriber : IMessageSubscriber
{
    public BackgroundServiceMessageSubscriber()
    {
    }

    // public async Task OnMessageReceived(string message, CancellationToken cancellationToken)
    // {
    //     if (_webSocket.State == WebSocketState.Open)
    //     {
    //         var messageBytes = Encoding.UTF8.GetBytes(message);
    //         await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, 
    //             true, cancellationToken);
    //     }
    //     else
    //     {
    //         throw new WebSocketException("The websocket is not open.");
    //     }
    // }
    public Task OnMessageReceived(string message, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
