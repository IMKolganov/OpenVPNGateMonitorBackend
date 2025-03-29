using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class OpenVpnServerOvpnFileConfigSeedData
{ 
    public static readonly OpenVpnServerOvpnFileConfig[] Data =
    {
        new OpenVpnServerOvpnFileConfig
        {
            Id = 1,
            VpnServerId = 1,
            VpnServerIp = "127.0.0.1",
            VpnServerPort = 1194,
            ConfigTemplate = @"client
dev tun
proto udp
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
</tls-crypt>"
        },
        new OpenVpnServerOvpnFileConfig
        {
            Id = 2,
            VpnServerId = 2,
            VpnServerIp = "127.0.0.1",
            VpnServerPort = 1195,
            ConfigTemplate = @"client
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
</tls-crypt>"
        },
    };
}
// verify-x509-name vpn-server name todo: added param for this
