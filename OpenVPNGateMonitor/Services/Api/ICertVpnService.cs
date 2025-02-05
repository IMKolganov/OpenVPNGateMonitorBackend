using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public interface ICertVpnService
{
    List<CertificateCaInfo> GetAllVpnCertificates();
    List<CertificateCaInfo> GetAllVpnCertificatesByStatus(CertificateStatus certificateStatus);
    CertificateBuildResult AddCertificate(string cnName, CancellationToken cancellationToken);
    public CertificateRevokeResult RemoveCertificate(string cnName, CancellationToken cancellationToken);
}