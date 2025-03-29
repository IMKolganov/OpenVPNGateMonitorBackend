using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using OpenVPNGateMonitor.Controllers;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.Models.Enums;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;
using Mapster;

namespace OpenVPNGateMonitor.Tests.Controllers
{
    public class OpenVpnServerCertsControllerTests
    {
        private readonly Mock<ILogger<OpenVpnServerCertsController>> _loggerMock;
        private readonly Mock<ICertVpnService> _certVpnServiceMock;
        private readonly OpenVpnServerCertsController _controller;

        public OpenVpnServerCertsControllerTests()
        {
            _loggerMock = new Mock<ILogger<OpenVpnServerCertsController>>();
            _certVpnServiceMock = new Mock<ICertVpnService>();
            _controller = new OpenVpnServerCertsController(_loggerMock.Object, _certVpnServiceMock.Object);
        }

        [Fact]
        public async Task GetAllVpnServerCertificates_ReturnsExpectedData()
        {
            // Arrange
            TypeAdapterConfig.GlobalSettings.NewConfig<CertificateCaInfo, VpnServerCertificateResponse>()
                .Map(dest => dest.CnName, src => src.CommonName)
                .Map(dest => dest.IsRevoked, src => src.Status == CertificateStatus.Revoked)
                .Map(dest => dest.IssuedAt, src => src.RevokeDate ?? DateTime.MinValue)
                .Map(dest => dest.CertificateData, src => src.SerialNumber)
                .Map(dest => dest.Id, _ => 0); // mock if necessary

            var request = new GetAllVpnServerCertificatesRequest { VpnServerId = 1 };
    
            var certList = new List<CertificateCaInfo>
            {
                new CertificateCaInfo
                {
                    CommonName = "cert1",
                    SerialNumber = "SN001",
                    Status = CertificateStatus.Active
                },
                new CertificateCaInfo
                {
                    CommonName = "cert2",
                    SerialNumber = "SN002",
                    Status = CertificateStatus.Revoked,
                    RevokeDate = new DateTime(2024, 01, 01)
                }
            };

            _certVpnServiceMock
                .Setup(s => s.GetAllVpnServerCertificates(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(certList);

            // Act
            var result = await _controller.GetAllVpnServerCertificates(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<VpnServerCertificateResponse>>>(okResult.Value);

            Assert.True(response.Success);
            Assert.Equal(2, response.Data!.Count);

            var cert1 = response.Data.FirstOrDefault(r => r.CnName == "cert1");
            var cert2 = response.Data.FirstOrDefault(r => r.CnName == "cert2");

            Assert.NotNull(cert1);
            Assert.Equal("SN001", cert1!.CertificateData);
            Assert.False(cert1.IsRevoked);

            Assert.NotNull(cert2);
            Assert.True(cert2!.IsRevoked);
            Assert.Equal("SN002", cert2.CertificateData);
            Assert.Equal(new DateTime(2024, 01, 01), cert2.IssuedAt);
        }


        [Fact]
        public async Task AddServerCertificate_ReturnsCreatedCertificate()
        {
            // Arrange
            var request = new AddServerCertificateRequest
            {
                VpnServerId = 10,
                CnName = "test-cert"
            };

            var certResult = new CertificateBuildResult
            {
                VpnServerId = 10,
                CertificatePath = "/etc/openvpn/test-cert.crt",
                KeyPath = "/etc/openvpn/test-cert.key",
                RequestPath = "/etc/openvpn/test-cert.req",
                PemPath = "/etc/openvpn/test-cert.pem",
                CertId = "cert-id-123"
            };

            _certVpnServiceMock
                .Setup(s => s.AddServerCertificate(10, "test-cert", It.IsAny<CancellationToken>()))
                .ReturnsAsync(certResult);

            // Act
            var result = await _controller.AddServerCertificate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<VpnServerCertificateResponse>>(okResult.Value);

            Assert.True(response.Success);
            // Assert.Equal("cert-id-123", response.Data.CertId);
            // Assert.Equal("/etc/openvpn/test-cert.crt", response.Data.CertificatePath);
            // Assert.Equal("/etc/openvpn/test-cert.key", response.Data.KeyPath);
            // Assert.Equal("/etc/openvpn/test-cert.req", response.Data.RequestPath);
            // Assert.Equal("/etc/openvpn/test-cert.pem", response.Data.PemPath);
            Assert.Equal(10, response.Data!.VpnServerId);
        }

        [Fact]
        public async Task RevokeServerCertificate_ReturnsRevokeStatus()
        {
            // Arrange
            var request = new RevokeCertificateRequest
            {
                VpnServerId = 5,
                CommonName = "expired-cert"
            };

            var revokeResult = new CertificateRevokeResult
            {
                IsRevoked = true,
                Message = "expired"
            };

            _certVpnServiceMock
                .Setup(s => s.RevokeServerCertificate(5, "expired-cert", It.IsAny<CancellationToken>()))
                .ReturnsAsync(revokeResult);

            // Act
            var result = await _controller.RevokeServerCertificate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<RevokeCertificateResponse>>(okResult.Value);
            Assert.True(response.Success);
            Assert.True(response.Data!.IsRevoked);
        }
    }
}
