namespace DataGateMonitor.SharedModels.DataGateXRayManager.Info;

public class ConfigInfoResponse
{
    public string? Dns1 { get; set; }
    public string? Dns2 { get; set; }

    /// <summary>
    /// Classic VPN DNS servers the client should push via the OS tunnel
    /// (<c>VpnService.addDnsServer</c> / equivalent). Built from <c>DNS1</c>/<c>DNS2</c>.
    /// VLESS does not push DNS like OpenVPN <c>dhcp-option DNS</c>.
    /// </summary>
    public List<string> ClientDnsServers { get; set; } = [];

    /// <summary>
    /// When true, node runs Xray DNS identity (<c>XRAY_DNS_IDENTITY_*</c>); clients must use
    /// <see cref="ClientDnsServers"/> (typically Pi-hole) over classic :53 through the tunnel.
    /// </summary>
    public bool DnsIdentityEnabled { get; set; }

    public string? VpnSubnet { get; set; }
    public string? VpnNetmask { get; set; }
    public string? DataDir { get; set; }
    public string? Port { get; set; }
    public string? ApiPort { get; set; }
    public string? Proto { get; set; }
    public XRayManagementInfoResponse XRayManagement { get; set; } = new();
    public string? BackendBaseUrl { get; set; }
}
