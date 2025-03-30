namespace OpenVPNGateMonitor.Models.Helpers.Services;

public class OpenVpnServerWithStatus
{
    public required OpenVpnServer OpenVpnServer { get; set; }
    public OpenVpnServerStatusLog? OpenVpnServerStatusLog { get; set; }
    public int CountConnectedClients  { get; set; }
    public int CountSessions { get; set; }
    public long TotalBytesIn { get; set; }
    public long TotalBytesOut { get; set; }
}