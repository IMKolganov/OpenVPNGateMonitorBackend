using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

public interface IEasyRsaService
{
    CertificateBuildResult BuildCertificate(OpenVpnServerCertConfig openVpnServerCertConfig,
        string baseFileName = "client1");
    string ReadPemContent(string filePath);
    CertificateRevokeResult RevokeCertificate(OpenVpnServerCertConfig openVpnServerCertConfig, string commonName);
    List<CertificateCaInfo> GetAllCertificateInfoInIndexFile(string pkiPath);
    bool CheckHealthFileSystem(OpenVpnServerCertConfig openVpnServerCertConfig);
}