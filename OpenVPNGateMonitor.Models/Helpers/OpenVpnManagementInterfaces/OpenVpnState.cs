namespace OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

public class OpenVpnState
{
    public DateTime UpSince { get; set; }
    public bool Connected { get; set; }
    public bool Success { get; set; }
    public string LocalIp { get; set; } = string.Empty;
    public string RemoteIp { get; set; } = string.Empty;
}
