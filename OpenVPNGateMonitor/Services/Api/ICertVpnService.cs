using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public interface ICertVpnService
{
    Task<List<CertificateCaInfo>> GetAllVpnCertificates(int vpnServerId,
        CancellationToken cancellationToken);
    Task<List<CertificateCaInfo>> GetAllVpnCertificatesByStatus(int vpnServerId,
        CertificateStatus certificateStatus, CancellationToken cancellationToken);
    Task<CertificateBuildResult> AddCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
    public Task<CertificateRevokeResult> RemoveCertificate(int vpnServerId, string cnName,
        CancellationToken cancellationToken);
}