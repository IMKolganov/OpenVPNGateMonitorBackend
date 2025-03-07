﻿namespace OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

public class OpenVpnClient
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public string CommonName { get; set; } = string.Empty;
    public string RemoteIp { get; set; } = string.Empty;
    public string LocalIp { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    private DateTime _connectedSince;
    public DateTime ConnectedSince
    {
        get => _connectedSince;
        set => _connectedSince = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
    public string Username { get; set; } = string.Empty;
    
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}