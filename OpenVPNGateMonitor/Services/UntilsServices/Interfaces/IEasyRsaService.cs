using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

public interface IEasyRsaService
{
    CertificateBuildResult BuildCertificate(string certName = "client1");
    string ReadPemContent(string filePath);
    CertificateRevokeResult RevokeCertificate(string clientName);
    List<CertificateCaInfo> GetAllCertificateInfoInIndexFile();
    bool CheckHealthFileSystem();
}