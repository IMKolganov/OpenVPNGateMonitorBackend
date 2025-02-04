using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.UntilsServices.Interfaces;

public interface IEasyRsaService
{
    void InstallEasyRsa();
    CertificateResult BuildCertificate(string certName = "client1");
    string ReadPemContent(string filePath);
    string RevokeCertificate(string clientName);
    List<CertificateCaInfo> GetAllCertificateInfoInIndexFile();
}