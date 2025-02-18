using System.Net.WebSockets;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnWebSocketHandler
{
    private readonly OpenVpnManagerPool _managerPool;
    private readonly ILogger<OpenVpnWebSocketHandler> _logger;

    public OpenVpnWebSocketHandler(OpenVpnManagerPool managerPool, ILogger<OpenVpnWebSocketHandler> logger)
    {
        _managerPool = managerPool;
        _logger = logger;
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket, string ip, int port, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"[WS] New WebSocket client connected to {ip}:{port}");

        var manager = _managerPool.GetOrCreateManager(ip, port);
        await manager.HandleWebSocketAsync(webSocket, cancellationToken);
    }
}