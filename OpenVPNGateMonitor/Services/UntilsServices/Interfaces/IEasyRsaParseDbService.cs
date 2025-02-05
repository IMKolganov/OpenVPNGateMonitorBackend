using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

public interface IEasyRsaParseDbService
{
    List<CertificateCaInfo> ParseCertificateInfoInIndexFile(string pkiPath);
}