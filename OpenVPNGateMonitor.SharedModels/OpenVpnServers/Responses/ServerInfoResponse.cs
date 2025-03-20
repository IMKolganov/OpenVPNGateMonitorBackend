namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class ServerInfoResponse
{
    public int ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsOnline { get; set; }
    public string? Status { get; set; }
    public long TotalBytesIn { get; set; }
    public long TotalBytesOut { get; set; }
    public string? Version { get; set; }
    public DateTime? UpSince { get; set; }
}