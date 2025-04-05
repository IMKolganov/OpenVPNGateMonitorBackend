using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.EasyRsaServices;
using OpenVPNGateMonitor.Services.EasyRsaServices.Interfaces;

namespace OpenVPNGateMonitor.Tests.Services;

public class EasyRsaServiceTests
{
    private readonly Mock<ILogger<IEasyRsaService>> _loggerMock = new();
    private readonly Mock<IEasyRsaParseDbService> _parseDbServiceMock = new();
    private readonly Mock<IEasyRsaExecCommandService> _execCommandServiceMock = new();

    private readonly OpenVpnServerCertConfig _config = new()
    {
        EasyRsaPath = "/easyrsa",
        PkiPath = "/easyrsa/pki",
        OvpnFileDir = "/ovpn",
        RevokedOvpnFilesDirPath = "/revoked",
        CrlPkiPath = "/easyrsa/pki/crl.pem",
        CrlOpenvpnPath = "/etc/openvpn/crl.pem",
        CaCertPath = "/easyrsa/pki/ca.crt",
        TlsAuthKey = "/easyrsa/pki/ta.key",
        VpnServerId = 1
    };

    private EasyRsaService CreateService() =>
        new EasyRsaService(_loggerMock.Object, new Mock<IConfiguration>().Object, _parseDbServiceMock.Object, _execCommandServiceMock.Object);

    [Fact]
    public void ReadPemContent_ShouldReturnOnlyCertificate()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "garbage\n-----BEGIN CERTIFICATE-----\ndata\n-----END CERTIFICATE-----\nextra");

        var service = CreateService();
        var result = service.ReadPemContent(tempFile);

        Assert.Contains("BEGIN CERTIFICATE", result);
        Assert.Contains("END CERTIFICATE", result);
        File.Delete(tempFile);
    }

    [Fact]
    public void CheckHealthFileSystem_ShouldThrowIfIndexFileMissing()
    {
        Directory.CreateDirectory(_config.OvpnFileDir);
        Directory.CreateDirectory(_config.RevokedOvpnFilesDirPath);
        Directory.CreateDirectory(_config.PkiPath);

        var service = CreateService();

        try
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                service.CheckHealthFileSystem(_config);
            });
        }
        finally
        {
            Directory.Delete(_config.OvpnFileDir, true);
            Directory.Delete(_config.RevokedOvpnFilesDirPath, true);
            Directory.Delete(_config.PkiPath, true);
        }
    }

    [Fact]
    public void BuildCertificate_ShouldReturnResult_WhenSuccessful()
    {
        var baseFileName = "client1";

        _execCommandServiceMock.Setup(x => x.RunCommand(It.IsAny<string>()))
            .Returns(("output", "", 0));

        _execCommandServiceMock.Setup(x => x.RunCommand(It.Is<string>(s => s.Contains("openssl"))))
            .Returns(("serial=ABC123", "", 0));

        _parseDbServiceMock.Setup(x => x.ParseCertificateInfoInIndexFile(It.IsAny<string>()))
            .Returns(new List<CertificateCaInfo>
            {
                new()
                {
                    Status = CertificateStatus.Active,
                    CommonName = baseFileName,
                    SerialNumber = "ABC123"
                }
            });

        var service = CreateService();

        var result = service.BuildCertificate(_config, baseFileName);

        Assert.Equal("ABC123", result.CertId);
        Assert.EndsWith("client1.crt", result.CertificatePath);
    }

    [Fact]
    public void BuildCertificate_ShouldThrow_WhenCertNotFound()
    {
        _execCommandServiceMock.Setup(x => x.RunCommand(It.IsAny<string>()))
            .Returns(("output", "", 0));

        _parseDbServiceMock.Setup(x => x.ParseCertificateInfoInIndexFile(It.IsAny<string>()))
            .Returns(new List<CertificateCaInfo>());

        var service = CreateService();

        Assert.Throws<Exception>(() => service.BuildCertificate(_config, "client1"));
    }

    [Fact]
    public void RevokeCertificate_ShouldRevokeSuccessfully()
    {
        var commonName = "client1";
        var certPath = Path.Combine(_config.PkiPath, "issued", $"{commonName}.crt");
        var crlPath = _config.CrlPkiPath;

        Directory.CreateDirectory(Path.GetDirectoryName(certPath)!);
        File.WriteAllText(certPath, "dummy");

        _execCommandServiceMock.Setup(x =>
                x.ExecuteEasyRsaCommand(It.IsAny<string>(), It.IsAny<string>(), true))
            .Returns((true, "revocation successful", 0, ""));

        _execCommandServiceMock.Setup(x =>
                x.ExecuteEasyRsaCommand("gen-crl", It.IsAny<string>(), false))
            .Returns((true, "crl generated", 0, ""));

        _execCommandServiceMock.Setup(x =>
                x.RunCommand(It.Is<string>(cmd => cmd.Contains("cp "))))
            .Returns(("OK", "", 0));

        File.WriteAllText(crlPath, "dummycrl");

        var service = CreateService();
        var result = service.RevokeCertificate(_config, commonName);

        Assert.True(result.IsRevoked);
        Assert.Contains("client1", result.CertificatePath);

        File.Delete(certPath);
        File.Delete(crlPath);
    }
}
