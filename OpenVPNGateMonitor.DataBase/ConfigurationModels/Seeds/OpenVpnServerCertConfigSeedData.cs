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
            EasyRsaPath = "/openvpn-upd/easy-rsa",
            OvpnFileDir = "/openvpn-upd/clients",
            RevokedOvpnFilesDirPath = "/openvpn-upd/clients/revoked/",
            PkiPath = "/openvpn-upd/easy-rsa/pki/",
            CaCertPath = "/openvpn-upd/easy-rsa/pki/ca.crt",
            TlsAuthKey = "/openvpn-upd/easy-rsa/pki/ta.key",
            ServerRemoteIp = "0.0.0.0",
            CrlPkiPath = "/openvpn-upd/easy-rsa/pki/crl.pem",
            CrlOpenvpnPath = "/openvpn-upd/crl.pem",
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