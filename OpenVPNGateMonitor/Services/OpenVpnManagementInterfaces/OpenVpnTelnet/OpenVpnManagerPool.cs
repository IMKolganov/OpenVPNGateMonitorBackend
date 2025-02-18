using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnManagerPool
{
    private readonly ILogger<OpenVpnManagerPool> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenVpnCommandQueue _openVpnCommandQueue;
    private readonly ConcurrentDictionary<string, OpenVpnManager> _managers = new();

    public OpenVpnManagerPool(ILogger<OpenVpnManagerPool> logger, ILoggerFactory loggerFactory, OpenVpnCommandQueue openVpnCommandQueue)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _openVpnCommandQueue = openVpnCommandQueue;
    }

    public OpenVpnManager GetOrCreateManager(string managementIp, int managementPort)
    {
        var key = $"{managementIp}:{managementPort}";
        return _managers.GetOrAdd(key, _ =>
        {
            _logger.LogInformation($"[POOL] Creating new OpenVPN Manager for {key}");
            var manager = new OpenVpnManager(managementIp, managementPort, _loggerFactory, _openVpnCommandQueue);

            manager.OnDisposed += disposedKey =>
            {
                _logger.LogInformation($"[POOL] Removing manager {disposedKey} due to inactivity.");
                _managers.TryRemove(disposedKey, out OpenVpnManager? _);
            };

            return manager;
        });
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket, string ip, int port, CancellationToken cancellationToken)
    {
        var manager = GetOrCreateManager(ip, port);
        await manager.HandleWebSocketAsync(webSocket, cancellationToken);
    }
}