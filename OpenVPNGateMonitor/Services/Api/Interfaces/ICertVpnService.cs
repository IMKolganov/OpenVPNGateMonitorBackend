using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api.Interfaces;

public interface ICertVpnService
{
    Task<List<CertificateCaInfo>> GetAllVpnServerCertificates(int vpnServerId,
        CancellationToken cancellationToken);
    Task<List<CertificateCaInfo>> GetAllVpnServerCertificatesByStatus(int vpnServerId,
        CertificateStatus certificateStatus, CancellationToken cancellationToken);
    Task<CertificateBuildResult> AddServerCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
    public Task<CertificateRevokeResult> RemoveServerCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
}