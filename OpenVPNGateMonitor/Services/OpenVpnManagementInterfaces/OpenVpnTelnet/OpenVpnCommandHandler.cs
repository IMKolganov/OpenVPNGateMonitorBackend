namespace OpenVPNGateMonitor.Services.OpenVpnManagementInterfaces.OpenVpnTelnet;

public class OpenVpnCommandHandler
{
    private readonly ILogger<OpenVpnCommandHandler> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _host;
    private readonly int _port;

    public OpenVpnCommandHandler(string host, int port, ILoggerFactory loggerFactory, ILogger<OpenVpnCommandHandler> logger)
    {
        _host = host;
        _port = port;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        using var telnetClient = new OpenVpnTelnetClient(_host, _port, _loggerFactory.CreateLogger<OpenVpnTelnetClient>());
        await telnetClient.ConnectAsync(cancellationToken);
        return await telnetClient.SendCommandAsync(command, cancellationToken);
    }
}