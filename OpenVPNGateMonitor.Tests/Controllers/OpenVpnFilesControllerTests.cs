using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenVPNGateMonitor.Controllers;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.Services.Api.Interfaces;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;
using OpenVPNGateMonitor.SharedModels.Responses;

namespace OpenVPNGateMonitor.Tests.Controllers;

public class OpenVpnFilesControllerTests
{
    private readonly Mock<IOvpnFileService> _fileServiceMock;
    private readonly OpenVpnFilesController _controller;

    public OpenVpnFilesControllerTests()
    {
        _fileServiceMock = new Mock<IOvpnFileService>();
        _controller = new OpenVpnFilesController(_fileServiceMock.Object);
    }

    [Fact]
    public async Task GetAllOvpnFiles_ReturnsExpectedResult()
    {
        var request = new GetAllOvpnFilesRequest { VpnServerId = 1 };

        var files = new List<IssuedOvpnFile>
        {
            new() { Id = 1, CommonName = "file1" },
            new() { Id = 2, CommonName = "file2" }
        };

        _fileServiceMock
            .Setup(s => s.GetAllOvpnFiles(request.VpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var result = await _controller.GetAllOvpnFiles(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<ApiResponse<List<OvpnFileResponse>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllByExternalIdOvpnFiles_ReturnsExpectedResult()
    {
        var request = new GetAllByExternalIdOvpnFilesRequest
        {
            VpnServerId = 1,
            ExternalId = "user1"
        };

        var files = new List<IssuedOvpnFile>
        {
            new() { Id = 1, CommonName = "client.ovpn" }
        };

        _fileServiceMock
            .Setup(s => s.GetAllOvpnFilesByExternalId(request.VpnServerId, request.ExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var result = await _controller.GetAllByExternalIdOvpnFiles(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<ApiResponse<List<OvpnFileResponse>>>().Subject;
        response.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task AddOvpnFile_ReturnsSuccessResponse()
    {
        var request = new AddOvpnFileRequest
        {
            VpnServerId = 1,
            ExternalId = "user123",
            CommonName = "client",
            IssuedTo = "Test User"
        };

        var fileResponse = new AddOvpnFileResponse
        {
            OvpnFile = new FileInfo("dummy.ovpn"),
            IssuedOvpnFile = new IssuedOvpnFile
            {
                Id = 1,
                CommonName = "client",
                ExternalId = request.ExternalId,
                ServerId = request.VpnServerId,
                IssuedTo = request.IssuedTo,
                FileName = "dummy.ovpn",
                IssuedAt = DateTime.UtcNow
            }
        };

        _fileServiceMock
            .Setup(s => s.AddOvpnFile(
                request.ExternalId,
                request.CommonName,
                request.VpnServerId,
                It.IsAny<CancellationToken>(),
                request.IssuedTo))
            .ReturnsAsync(fileResponse);

        var result = await _controller.AddOvpnFile(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<ApiResponse<AddOvpnFileApiResponse>>().Subject;

        response.Success.Should().BeTrue();
        response.Data!.FileName.Should().Be("dummy.ovpn");
        response.Data.Metadata.CommonName.Should().Be("client");
        response.Data.Metadata.ExternalId.Should().Be("user123");
    }

    [Fact]
    public async Task RevokeOvpnFile_ReturnsSuccess_WhenNotAlreadyRevoked()
    {
        var request = new RevokeOvpnFileRequest
        {
            ServerId = 1,
            CommonName = "client1",
            ExternalId = "user1"
        };

        // result == null means success (as per controller logic)
        _fileServiceMock
            .Setup(s => s.RevokeOvpnFile(It.IsAny<IssuedOvpnFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssuedOvpnFile?)null);

        var result = await _controller.RevokeOvpnFile(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeAssignableTo<ApiResponse<RevokeOvpnFileResponse>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeOvpnFile_ReturnsNotFound_WhenAlreadyRevoked()
    {
        var request = new RevokeOvpnFileRequest
        {
            ServerId = 1,
            CommonName = "client1",
            ExternalId = "user1"
        };

        var alreadyRevoked = new IssuedOvpnFile { Id = 99 };

        _fileServiceMock
            .Setup(s => s.RevokeOvpnFile(It.IsAny<IssuedOvpnFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyRevoked);

        var result = await _controller.RevokeOvpnFile(request, CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFound.Value.Should().BeAssignableTo<ApiResponse<RevokeOvpnFileResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadOvpnFile_ReturnsFile_WhenFound()
    {
        var request = new DownloadOvpnFileRequest
        {
            IssuedOvpnFileId = 1,
            VpnServerId = 1
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "dummy test file");
        var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read);

        var fileResult = new OvpnFileResult
        {
            FileStream = fileStream,
            FileName = "test.ovpn"
        };

        _fileServiceMock
            .Setup(s => s.GetOvpnFile(request.IssuedOvpnFileId, request.VpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileResult);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await _controller.DownloadOvpnFile(request, CancellationToken.None);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.ContentType.Should().Be("application/x-openvpn-profile");
        file.FileDownloadName.Should().Be("test.ovpn");

        await fileStream.DisposeAsync();
        File.Delete(tempFile);
    }

    
    [Fact]
    public async Task DownloadOvpnFile_ReturnsNotFound_WhenMissing()
    {
        var request = new DownloadOvpnFileRequest
        {
            IssuedOvpnFileId = 1,
            VpnServerId = 1
        };

        var fileResult = new OvpnFileResult
        {
            FileStream = null,
            FileName = "missing.ovpn"
        };

        _fileServiceMock
            .Setup(s => s.GetOvpnFile(request.IssuedOvpnFileId, request.VpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileResult);

        var result = await _controller.DownloadOvpnFile(request, CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFound.Value.Should().BeAssignableTo<ApiResponse<string>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("missing.ovpn");
    }
}
