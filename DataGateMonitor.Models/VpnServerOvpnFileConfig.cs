using System.ComponentModel.DataAnnotations;

namespace DataGateMonitor.Models;

/// <summary>
/// Per-server template and public endpoint hints for <b>client exports</b>: OpenVPN .ovpn layout or
/// Xray/VLESS link text (placeholders such as <c>{{vless_uri}}</c>, <c>{{uuid}}</c>). Table name is historical.
/// </summary>
public class VpnServerOvpnFileConfig : BaseEntity<int>
{
    [Required]
    public int VpnServerId { get; set; }

    [Required]
    public string VpnServerIp { get; set; } = string.Empty;

    public int VpnServerPort { get; set; } = 1194;

    public string ConfigTemplate { get; set; } = @"setenv FRIENDLY_NAME ""{{friendly_name}}""
client
dev tun
proto tcp
remote {{server_ip}} {{server_port}}
resolv-retry infinite
nobind
remote-cert-tls server
tls-version-min 1.2
cipher AES-256-CBC
auth SHA256
auth-nocache
verb 3
<ca>
{{ca_cert}}
</ca>
<cert>
{{client_cert}}
</cert>
<key>
{{client_key}}
</key>
<tls-crypt>
{{tls_auth_key}}
</tls-crypt>";

    public void SetDefault()
    {
        VpnServerPort = 1194;
        VpnServerIp = string.Empty;
        ConfigTemplate = @"setenv FRIENDLY_NAME ""{{friendly_name}}""
client
dev tun
proto tcp
remote {{server_ip}} {{server_port}}
resolv-retry infinite
nobind
remote-cert-tls server
tls-version-min 1.2
cipher AES-256-CBC
auth SHA256
auth-nocache
verb 3
<ca>
{{ca_cert}}
</ca>
<cert>
{{client_cert}}
</cert>
<key>
{{client_key}}
</key>
<tls-crypt>
{{tls_auth_key}}
</tls-crypt>";
    }

    public string RenderTemplate()
    {
        return ConfigTemplate
            .Replace("{{server_ip}}", VpnServerIp)
            .Replace("{{server_port}}", VpnServerPort.ToString());
    }
}