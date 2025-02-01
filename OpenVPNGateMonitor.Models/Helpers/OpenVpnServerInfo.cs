namespace OpenVPNGateMonitor.Models.Helpers;

public class OpenVpnServerInfo {
    
    public string VpnMode { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;

    public bool Pingable { get; set; } = false;

    public int Clients { get; set; } = 0;

    public int TotalBytesIn { get; set; } = 0;

    public int TotalBytesOut { get; set; } = 0;

    public string UpSince { get; set; } = string.Empty;

    public string LocalIpAddress { get; set; } = string.Empty;
}