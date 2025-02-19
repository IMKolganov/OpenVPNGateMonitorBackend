using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

namespace OpenVPNGateMonitor.Controllers;

[Route("api/openvpn")]
[ApiController]
public class OpenVpnWebSocketController : ControllerBase
{
    private readonly ICommandQueueManager _commandQueueManager;
    private readonly ILogger<OpenVpnWebSocketController> _logger;

    public OpenVpnWebSocketController(ICommandQueueManager commandQueueManager, ILogger<OpenVpnWebSocketController> logger)
    {
        _commandQueueManager = commandQueueManager;
        _logger = logger;
    }

    [HttpGet("ws/{ip}/{port}")]
    public async Task GetWebSocket(string ip, int port)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation($"WebSocket connection established for {ip}:{port}");

            var commandQueue = await _commandQueueManager.GetOrCreateQueueAsync(ip, port);

            await HandleWebSocketCommunication(webSocket, commandQueue, ip, port);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleWebSocketCommunication(WebSocket webSocket, CommandQueue commandQueue, string ip, int port)
    {
        var buffer = new byte[1024 * 4];
        var webSocketSubscriber = new WebSocketMessageSubscriber(webSocket);
        commandQueue.Subscribe(webSocketSubscriber);
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var command = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received command: {command}");

                    try
                    {
                        var response = await commandQueue.SendCommandAsync(command);
                        Console.WriteLine($"Response: {response}");

                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing command: {ex.Message}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket closed by client.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
        }
        finally
        {
            await commandQueue.Unsubscribe(webSocketSubscriber, ip, port, _commandQueueManager);

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            Console.WriteLine("WebSocket connection closed.");
        }
    }

}