namespace OpenVPNGateMonitor.SharedModels.OpenVpnServers.Responses;

public class VpnClientInfoResponse
{
    public int VpnServerId { get; set; }
    public Guid SessionId { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string RemoteIp { get; set; } = string.Empty;
    public string LocalIp { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public DateTime ConnectedSince { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsConnected { get; set; }
}