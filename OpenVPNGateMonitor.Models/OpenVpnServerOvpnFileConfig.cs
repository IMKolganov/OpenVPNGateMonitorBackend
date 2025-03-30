using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class OpenVpnServerOvpnFileConfig : BaseEntity<int>
{
    [Required]
    public int VpnServerId { get; set; }
    [Required]
    public string VpnServerIp { get; set; } = string.Empty;
    public int VpnServerPort { get; set; } = 1194;
    public string ConfigTemplate { get; set; } = @"client
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