using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

public interface IEasyRsaParseDbService
{
    List<CertificateCaInfo> ParseCertificateInfoInIndexFile(string pkiPath);
}