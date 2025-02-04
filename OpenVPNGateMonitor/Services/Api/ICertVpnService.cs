using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public interface ICertVpnService
{
    List<CertificateCaInfo> GetAllCertVpnCertificates();
    List<CertificateCaInfo> GetAllCertVpnCertificatesByStatus(CertificateStatus certificateStatus);
}