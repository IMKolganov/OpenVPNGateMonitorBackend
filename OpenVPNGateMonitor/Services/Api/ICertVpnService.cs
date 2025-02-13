using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public interface ICertVpnService
{
    List<CertificateCaInfo> GetAllVpnCertificates(int vpnServerId);
    List<CertificateCaInfo> GetAllVpnCertificatesByStatus(int vpnServerId, CertificateStatus certificateStatus);
    CertificateBuildResult AddCertificate(string cnName, CancellationToken cancellationToken);
    public CertificateRevokeResult RemoveCertificate(string cnName, CancellationToken cancellationToken);
}