namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerOvpnFileConfig.Responses;

public class OvpnFileConfigResponse
{
    public int VpnServerId { get; set; }
    public string VpnServerIp { get; set; } = string.Empty;
    public int VpnServerPort { get; set; }
    public string ConfigTemplate { get; set; } = string.Empty;
}