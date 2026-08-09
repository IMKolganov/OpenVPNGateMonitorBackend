using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.Responses;
using Xunit;

namespace DataGateMonitor.Tests.Controllers;

/// <summary>TV device-linking endpoints on <see cref="AuthController"/>.</summary>
public class AuthControllerTvLoginTests
{
    private readonly Mock<ITvLoginSessionService> _tv = new();

    private AuthController CreateController()
    {
        // Reuse the full AuthController constructor via AuthControllerTests helpers would be heavy;
        // build a minimal controller through the existing test factory pattern.
        return AuthControllerTvTestFactory.Create(_tv);
    }

    [Fact]
    public async Task CreateTvLoginSession_NullBody_UsesEmptyRequest_AndReturnsOk()
    {
        var created = new CreateTvLoginSessionResponse
        {
            SessionId = Guid.NewGuid(),
            UserCode = "123456",
            VerificationUrl = "https://tv-link.test/tv/link",
            QrPayload = "https://tv-link.test/tv/link?code=123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            PollIntervalSeconds = 2,
            SignalRHubPath = "/api/hubs/tv-login",
        };
        _tv.Setup(s => s.CreateSessionAsync(
                It.IsAny<CreateTvLoginSessionRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController();
        var result = await controller.CreateTvLoginSession(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<ApiResponse<CreateTvLoginSessionResponse>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("123456", envelope.Data!.UserCode);
        Assert.Equal("/api/hubs/tv-login", envelope.Data.SignalRHubPath);
    }

    [Fact]
    public async Task PollTvLoginSession_ReturnsStatusEnvelope()
    {
        var sessionId = Guid.NewGuid();
        _tv.Setup(s => s.PollSessionAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSessionPollResponse
            {
                Status = "viewed",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3),
            });

        var controller = CreateController();
        var result = await controller.PollTvLoginSession(sessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<ApiResponse<TvLoginSessionPollResponse>>(ok.Value);
        Assert.Equal("viewed", envelope.Data!.Status);
    }

    [Fact]
    public async Task GetTvLoginSessionByCode_WhenOk_ReturnsPreview()
    {
        _tv.Setup(s => s.GetByUserCodeAsync("123456", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSessionPreviewResponse
            {
                SessionId = Guid.NewGuid(),
                UserCode = "123456",
                Status = "pending",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
            });

        var controller = CreateController();
        var result = await controller.GetTvLoginSessionByCode("123456", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(((ApiResponse<TvLoginSessionPreviewResponse>)ok.Value!).Success);
    }

    [Fact]
    public async Task GetTvLoginSessionByCode_WhenNotFound_Returns404()
    {
        _tv.Setup(s => s.GetByUserCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(TvLoginSessionService.SessionNotFoundMessage));

        var controller = CreateController();
        var result = await controller.GetTvLoginSessionByCode("000000", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.False(((ApiResponse<TvLoginSessionPreviewResponse>)notFound.Value!).Success);
    }

    [Fact]
    public async Task GetTvLoginSessionByCode_WhenExpired_Returns410()
    {
        _tv.Setup(s => s.GetByUserCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(TvLoginSessionService.SessionExpiredMessage));

        var controller = CreateController();
        var result = await controller.GetTvLoginSessionByCode("111111", CancellationToken.None);

        var gone = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status410Gone, gone.StatusCode);
    }

    [Fact]
    public async Task ApproveTvLoginSession_DelegatesToService()
    {
        _tv.Setup(s => s.ApproveAsync(
                It.IsAny<ApproveTvLoginSessionRequest>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSessionActionResponse { Status = "approved" });

        var controller = CreateController();
        AuthControllerTvTestFactory.SetCurrentUserId(controller, 42);

        var result = await controller.ApproveTvLoginSession(
            new ApproveTvLoginSessionRequest { UserCode = "123456" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("approved", ((ApiResponse<TvLoginSessionActionResponse>)ok.Value!).Data!.Status);
        _tv.Verify(s => s.ApproveAsync(
            It.Is<ApproveTvLoginSessionRequest>(r => r.UserCode == "123456"),
            42,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DenyTvLoginSession_DelegatesToService()
    {
        _tv.Setup(s => s.DenyAsync(
                It.IsAny<DenyTvLoginSessionRequest>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TvLoginSessionActionResponse { Status = "denied" });

        var controller = CreateController();
        AuthControllerTvTestFactory.SetCurrentUserId(controller, 7);

        var result = await controller.DenyTvLoginSession(
            new DenyTvLoginSessionRequest { SessionId = Guid.NewGuid() },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("denied", ((ApiResponse<TvLoginSessionActionResponse>)ok.Value!).Data!.Status);
    }

    [Fact]
    public async Task GetTvLoginSessionByCode_WhenDenied_Returns410()
    {
        _tv.Setup(s => s.GetByUserCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(TvLoginSessionService.SessionDeniedMessage));

        var controller = CreateController();
        var result = await controller.GetTvLoginSessionByCode("111111", CancellationToken.None);

        var gone = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status410Gone, gone.StatusCode);
    }

    [Fact]
    public async Task GetTvLoginSessionByCode_WhenRateLimited_Returns429()
    {
        _tv.Setup(s => s.GetByUserCodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(TvLoginSessionService.RateLimitMessage));

        var controller = CreateController();
        var result = await controller.GetTvLoginSessionByCode("111111", CancellationToken.None);

        var tooMany = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooMany.StatusCode);
    }

    [Fact]
    public async Task CreateTvLoginSession_ForwardsRemoteIpAddress()
    {
        _tv.Setup(s => s.CreateSessionAsync(
                It.IsAny<CreateTvLoginSessionRequest>(),
                "203.0.113.10",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateTvLoginSessionResponse
            {
                SessionId = Guid.NewGuid(),
                UserCode = "123456",
                VerificationUrl = "https://tv-link.test/tv/link",
                QrPayload = "https://tv-link.test/tv/link?code=123456",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            });

        var controller = CreateController();
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");

        await controller.CreateTvLoginSession(new CreateTvLoginSessionRequest(), CancellationToken.None);

        _tv.Verify(s => s.CreateSessionAsync(
            It.IsAny<CreateTvLoginSessionRequest>(),
            "203.0.113.10",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TvLoginEndpoints_HaveExpectedAuthAttributes()
    {
        var type = typeof(AuthController);

        Assert.NotNull(type.GetMethod(nameof(AuthController.CreateTvLoginSession))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true)
            .FirstOrDefault());
        Assert.NotNull(type.GetMethod(nameof(AuthController.PollTvLoginSession))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true)
            .FirstOrDefault());
        Assert.NotNull(type.GetMethod(nameof(AuthController.GetTvLoginSessionByCode))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .FirstOrDefault());
        Assert.NotNull(type.GetMethod(nameof(AuthController.ApproveTvLoginSession))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .FirstOrDefault());
        Assert.NotNull(type.GetMethod(nameof(AuthController.DenyTvLoginSession))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .FirstOrDefault());
    }
}
