using Microsoft.AspNetCore.Mvc;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Tests.Controllers
{
    public class ApplicationsControllerTests
    {
        private readonly Mock<IApplicationService> appServiceMock;
        private readonly ApplicationsController controller;

        public ApplicationsControllerTests()
        {
            appServiceMock = new Mock<IApplicationService>();
            controller = new ApplicationsController(appServiceMock.Object);
        }

        // -------------------------
        // RegisterApplication tests
        // -------------------------

        [Fact]
        public async Task RegisterApplication_ReturnsOk_WithSuccessResponse()
        {
            // Arrange
            var request = new RegisterApplicationRequest
            {
                Name = "TestApp"
            };

            var newApp = new ClientApplication
            {
                ClientId = "client123",
                ClientSecret = "secret123",
                Name = "TestApp",
                IsRevoked = false,
                IsSystem = false
            };

            appServiceMock
                .Setup(s => s.RegisterApplicationAsync(request.Name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(newApp);

            var ct = CancellationToken.None;

            // Act
            var result = await controller.RegisterApplication(request, ct);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RegisterApplicationResponse>>(ok.Value);

            Assert.True(response.Success);
            Assert.Equal("Success", response.Message);

            Assert.NotNull(response.Data);
            Assert.Equal(newApp.ClientId, response.Data.ClientId);
            Assert.Equal(newApp.Name, response.Data.Name);

            appServiceMock.Verify(
                s => s.RegisterApplicationAsync(request.Name, ct),
                Times.Once);
        }

        // -------------------------
        // GetAllApplications tests
        // -------------------------

        [Fact]
        public async Task GetAllApplications_ReturnsOk_WithSuccessResponse()
        {
            // Arrange
            var app1 = new ClientApplication
            {
                ClientId = "c1",
                ClientSecret = "s1",
                Name = "App1",
                IsRevoked = false,
                IsSystem = false
            };

            var app2 = new ClientApplication
            {
                ClientId = "c2",
                ClientSecret = "s2",
                Name = "App2",
                IsRevoked = false,
                IsSystem = false
            };

            var list = new List<ClientApplication> { app1, app2 };

            appServiceMock
                .Setup(s => s.GetAllApplicationsAsync(It.IsAny<GetAllApplicationsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            var ct = CancellationToken.None;

            // Act
            var result = await controller.GetAllApplications(new GetAllApplicationsRequest(), ct);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<ApplicationsResponse>>(ok.Value);

            Assert.True(response.Success);
            Assert.Equal("Success", response.Message);
            Assert.NotNull(response.Data);

            // If ApplicationsResponse has collection — check size
            // (adjust property name if needed)
            Assert.Equal(2, response.Data.Applications.Count);

            appServiceMock.Verify(
                s => s.GetAllApplicationsAsync(It.IsAny<GetAllApplicationsRequest>(), ct),
                Times.Once);
        }

    [Fact]
    public async Task GetAllApplications_Passes_Filter_Request_To_Service()
    {
        var request = new GetAllApplicationsRequest { Name = "App", IsRevoked = false };
        appServiceMock
            .Setup(s => s.GetAllApplicationsAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ClientApplication>());

        await controller.GetAllApplications(request, CancellationToken.None);

        appServiceMock.Verify(s => s.GetAllApplicationsAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------
    // RevokeApplication tests
    // -------------------------

        [Fact]
        public async Task RevokeApplication_WhenExists_ReturnsOk()
        {
            // Arrange
            var request = new RevokeApplicationRequest
            {
                ClientId = "client123"
            };

            appServiceMock
                .Setup(s => s.RevokeApplicationAsync(request.ClientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var ct = CancellationToken.None;

            // Act
            var result = await controller.RevokeApplication(request, ct);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<string>>(ok.Value);

            Assert.True(response.Success);
            Assert.Equal("Success", response.Message);
            Assert.Equal("Application revoked", response.Data);

            appServiceMock.Verify(
                s => s.RevokeApplicationAsync(request.ClientId, ct),
                Times.Once);
        }

        [Fact]
        public async Task RevokeApplication_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            var request = new RevokeApplicationRequest
            {
                ClientId = "missing123"
            };

            appServiceMock
                .Setup(s => s.RevokeApplicationAsync(request.ClientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var ct = CancellationToken.None;

            // Act
            var result = await controller.RevokeApplication(request, ct);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<string>>(notFound.Value);

            Assert.False(response.Success);
            Assert.Equal("Application not found", response.Message);
            Assert.Null(response.Data);

            appServiceMock.Verify(
                s => s.RevokeApplicationAsync(request.ClientId, ct),
                Times.Once);
        }

        [Fact]
        public async Task RegisterApplication_WhenEmptyName_ReturnsBadRequest()
        {
            var request = new RegisterApplicationRequest { Name = "  " };

            appServiceMock
                .Setup(s => s.RegisterApplicationAsync(request.Name, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("API client name is required."));

            var result = await controller.RegisterApplication(request, CancellationToken.None);

            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RegisterApplicationResponse>>(bad.Value);
            Assert.False(response.Success);
            Assert.Equal("API client name is required.", response.Message);
        }

        [Fact]
        public async Task RegisterApplication_WhenDuplicateName_ReturnsConflict()
        {
            var request = new RegisterApplicationRequest { Name = "TestApp" };

            appServiceMock
                .Setup(s => s.RegisterApplicationAsync(request.Name, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("An API client with this name already exists."));

            var result = await controller.RegisterApplication(request, CancellationToken.None);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<RegisterApplicationResponse>>(conflict.Value);
            Assert.False(response.Success);
            Assert.Equal("An API client with this name already exists.", response.Message);
        }

        [Fact]
        public async Task RevokeApplication_WhenSystem_ReturnsBadRequest()
        {
            var request = new RevokeApplicationRequest { ClientId = "system-client" };

            appServiceMock
                .Setup(s => s.RevokeApplicationAsync(request.ClientId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("System API clients cannot be revoked."));

            var result = await controller.RevokeApplication(request, CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<string>>(badRequest.Value);
            Assert.False(response.Success);
            Assert.Equal("System API clients cannot be revoked.", response.Message);
        }
    }
}
