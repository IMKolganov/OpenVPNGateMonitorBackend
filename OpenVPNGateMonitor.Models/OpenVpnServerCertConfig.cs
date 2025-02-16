using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.Models;

public class OpenVpnServerCertConfig
{
    public int Id { get; set; }
    [Required]
    public int VpnServerId { get; set; }
    public string EasyRsaPath { get; set; } = "/etc/openvpn/easy-rsa";
    public string OvpnFileDir { get; set; } = "/etc/openvpn/clients";
    public string RevokedOvpnFilesDirPath { get; set; } = "/etc/openvpn/clients/revoked/";
    public string PkiPath { get; set; } = "/etc/openvpn/easy-rsa/pki/";
    public string CaCertPath { get; set; } = "/etc/openvpn/easy-rsa/pki/ca.crt";
    public string TlsAuthKey { get; set; } = "/etc/openvpn/easy-rsa/pki/ta.key";
    public string ServerRemoteIp { get; set; } = "0.0.0.0";
    public string CrlPkiPath { get; set; } = "/etc/openvpn/easy-rsa/pki/crl.pem";
    public string CrlOpenvpnPath { get; set; } = "/etc/openvpn/crl.pem";
    public string StatusFilePath { get; set; } = "/var/log/openvpn-status.log";
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public DateTime CreateDate { get; set; }= DateTime.Now;
}