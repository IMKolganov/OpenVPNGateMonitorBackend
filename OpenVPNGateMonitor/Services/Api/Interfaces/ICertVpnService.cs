using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.Services;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface ICertVpnService
{
    Task<List<CertificateCaInfo>> GetAllVpnServerCertificates(int vpnServerId,
        CancellationToken cancellationToken);
    Task<List<CertificateCaInfo>> GetAllVpnServerCertificatesByStatus(int vpnServerId,
        CertificateStatus certificateStatus, CancellationToken cancellationToken);
    Task<CertificateBuildResult> AddServerCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
    Task<CertificateRevokeResult> RevokeServerCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
    Task<OpenVpnServerCertConfig> GetOpenVpnServerCertConf(int vpnServerId,
        CancellationToken cancellationToken);
    Task<OpenVpnServerCertConfig> UpdateServerCertConfig(
        OpenVpnServerCertConfigInfo openVpnServerCertConfigInfo,
        CancellationToken cancellationToken);
}