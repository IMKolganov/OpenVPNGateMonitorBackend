namespace DataGateMonitor.SharedModels.DataGateOpenVpnManager.Info;

public class ConfigInfoResponse
{
    public string? Dns1 { get; set; }
    public string? Dns2 { get; set; }
    public string? VpnSubnet { get; set; }
    public string? VpnNetmask { get; set; }
    public string? EasyRsaPath { get; set; }
    public string? DataDir { get; set; }
    public string? Port { get; set; }
    public string? ApiPort { get; set; }
    public string? Proto { get; set; }
    /// <summary>OpenVPN data-channel cipher from node env <c>CIPHER</c> (e.g. AES-128-GCM).</summary>
    public string? Cipher { get; set; }
    /// <summary>OpenVPN <c>data-ciphers</c> list from node env <c>DATA_CIPHERS</c>.</summary>
    public string? DataCiphers { get; set; }
    /// <summary>Whether DCO is enabled on the node (<c>DCO</c> env).</summary>
    public string? Dco { get; set; }
    public OpenVpnManagementInfoResponse? OpenVpnManagement { get; set; }
    public string? BackendBaseUrl { get; set; }
}
