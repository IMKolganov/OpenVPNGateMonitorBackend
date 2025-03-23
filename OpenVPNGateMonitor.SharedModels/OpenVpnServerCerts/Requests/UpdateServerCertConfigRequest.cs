using System.ComponentModel.DataAnnotations;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;

public class UpdateServerCertConfigRequest
{
    [Required(ErrorMessage = "vpnServerId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "vpnServerId must be greater than 0.")]
    public int VpnServerId { get; set; }

    public string EasyRsaPath { get; set; } = string.Empty;
    public string OvpnFileDir { get; set; } = string.Empty;
    public string RevokedOvpnFilesDirPath { get; set; } = string.Empty;
    public string PkiPath { get; set; } = string.Empty;
    public string CaCertPath { get; set; } = string.Empty;
    public string TlsAuthKey { get; set; } = string.Empty;
    public string ServerRemoteIp { get; set; } = string.Empty;
    public string CrlPkiPath { get; set; } = string.Empty;
    public string CrlOpenvpnPath { get; set; } = string.Empty;
    public string StatusFilePath { get; set; } = string.Empty;
}