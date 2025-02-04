using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api;

public interface ICertVpnService
{
    List<CertificateCaInfo> GetAllVpnCertificates();
    List<CertificateCaInfo> GetAllVpnCertificatesByStatus(CertificateStatus certificateStatus);
}