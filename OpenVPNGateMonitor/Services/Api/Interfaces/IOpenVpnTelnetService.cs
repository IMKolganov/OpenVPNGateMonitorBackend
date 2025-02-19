namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface IOpenVpnTelnetService
{
    Task HandleWebSocketAsync(HttpContext context, string ip, int port);
    Task HandleWebSocketByServerIdAsync(HttpContext context, int openVpnServerId);

}