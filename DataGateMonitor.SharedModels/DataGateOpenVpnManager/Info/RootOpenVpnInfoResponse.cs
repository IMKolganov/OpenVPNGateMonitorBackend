namespace DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;

public class RootOpenVpnInfoResponse
{
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Public WAN IP as seen from the node (best-effort).</summary>
    public string? PublicIp { get; set; }
    public ConfigInfoResponse Config { get; set; } = new();
}
