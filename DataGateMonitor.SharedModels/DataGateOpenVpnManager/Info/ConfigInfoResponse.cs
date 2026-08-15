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
    /// <summary>HMAC digest from node env <c>AUTH</c> (e.g. SHA256).</summary>
    public string? Auth { get; set; }
    /// <summary>Minimum TLS version from node env <c>TLS_VERSION_MIN</c> (e.g. 1.2).</summary>
    public string? TlsVersionMin { get; set; }
    /// <summary>Optional pushed MSSFIX value from node env <c>MSSFIX</c> (informational; not written into client .ovpn).</summary>
    public string? MssFix { get; set; }
    /// <summary>Suggested client <c>verb</c> from node env <c>CLIENT_VERB</c> (default 3; not the server log verb).</summary>
    public string? ClientVerb { get; set; }
    public OpenVpnManagementInfoResponse? OpenVpnManagement { get; set; }
    public string? BackendBaseUrl { get; set; }
}
