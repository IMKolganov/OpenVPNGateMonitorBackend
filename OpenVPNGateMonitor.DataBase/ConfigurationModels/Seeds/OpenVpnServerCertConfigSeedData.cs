using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class OpenVpnServerCertConfigSeedData
{ 
    public static readonly OpenVpnServerCertConfig[] Data =
    {
        new OpenVpnServerCertConfig
        {
            Id = 1,
            VpnServerId = 1,
            EasyRsaPath = "/openvpn-udp/easy-rsa",
            OvpnFileDir = "/openvpn-udp/clients",
            RevokedOvpnFilesDirPath = "/openvpn-udp/clients/revoked/",
            PkiPath = "/openvpn-udp/easy-rsa/pki/",
            CaCertPath = "/openvpn-udp/easy-rsa/pki/ca.crt",
            TlsAuthKey = "/openvpn-udp/easy-rsa/pki/ta.key",
            ServerRemoteIp = "0.0.0.0",
            CrlPkiPath = "/openvpn-udp/easy-rsa/pki/crl.pem",
            CrlOpenvpnPath = "/openvpn-udp/crl.pem",
            StatusFilePath = "/var/log/openvpn-status.log",
        },
        new OpenVpnServerCertConfig
        {
            Id = 2,
            VpnServerId = 2,
            EasyRsaPath = "/openvpn-tcp/easy-rsa",
            OvpnFileDir = "/openvpn-tcp/clients",
            RevokedOvpnFilesDirPath = "/openvpn-tcp/clients/revoked/",
            PkiPath = "/openvpn-tcp/easy-rsa/pki/",
            CaCertPath = "/openvpn-tcp/easy-rsa/pki/ca.crt",
            TlsAuthKey = "/openvpn-tcp/easy-rsa/pki/ta.key",
            ServerRemoteIp = "0.0.0.0",
            CrlPkiPath = "/openvpn-tcp/easy-rsa/pki/crl.pem",
            CrlOpenvpnPath = "/openvpn-tcp/crl.pem",
            StatusFilePath = "/var/log/openvpn-status.log",
        },
    };
}