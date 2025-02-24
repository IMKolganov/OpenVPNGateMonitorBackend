using System.Net.WebSockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;
using OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet.Subscribers;

namespace OpenVPNGateMonitor.Services.Api;

public class OpenVpnTelnetService : IOpenVpnTelnetService
{
    private readonly ILogger<OpenVpnTelnetService> _logger;
    private readonly ICommandQueueManager _commandQueueManager;
    private readonly IUnitOfWork _unitOfWork;

    
    public OpenVpnTelnetService(ILogger<OpenVpnTelnetService> logger, ICommandQueueManager commandQueueManager,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _commandQueueManager = commandQueueManager;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleWebSocketAsync(HttpContext context, string ip, int port, 
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation($"WebSocket connection established for {ip}:{port}");

        var commandQueue = await _commandQueueManager.GetOrCreateQueueAsync(ip, port, cancellationToken);
        var webSocketSubscriber = new WebSocketMessageSubscriber(webSocket);
        commandQueue.Subscribe(webSocketSubscriber);

        await HandleWebSocketCommunication(webSocket, commandQueue, ip, port);
    }

    public async Task HandleWebSocketByServerIdAsync(HttpContext context, int openVpnServerId, 
        CancellationToken cancellationToken)
    {
        var openVpnServer = await _unitOfWork.GetQuery<OpenVpnServer>()
            .AsQueryable()
            .Where(x => x.Id == openVpnServerId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken)??
            throw new InvalidOperationException("OpenVpnServerCertConfig not found");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }
        
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation($"WebSocket connection established for " +
                               $"{openVpnServer.ManagementIp}:{openVpnServer.ManagementPort}");
        
        var commandQueue = await _commandQueueManager.GetOrCreateQueueAsync(openVpnServer.ManagementIp, 
            openVpnServer.ManagementPort, cancellationToken: cancellationToken);
        var webSocketSubscriber = new WebSocketMessageSubscriber(webSocket);
        commandQueue.Subscribe(webSocketSubscriber);
        
        await HandleWebSocketCommunication(webSocket, commandQueue, openVpnServer.ManagementIp, 
            openVpnServer.ManagementPort);
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