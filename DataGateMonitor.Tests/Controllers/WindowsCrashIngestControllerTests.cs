using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DataGateMonitor.Configurations;
using DataGateMonitor.Controllers;
using DataGateMonitor.Services.Api.MobileCrashIngest;
using DataGateMonitor.Services.Api.WindowsCrashIngest;

namespace DataGateMonitor.Tests.Controllers;

public class WindowsCrashIngestControllerTests
{
    private static void AttachHttpsRequest(
        WindowsCrashIngestController controller,
        string body,
        string process = "com.imkolganov.datagate.win")
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Scheme = "https",
                ContentType = "text/plain; charset=utf-8",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
            },
        };
        context.Request.Headers["X-Crash-Filename"] = "win_crash_2026-05-01T00-00-00.000Z.txt";
        context.Request.Headers["X-Crash-Process"] = process;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    private static WindowsCrashIngestController CreateController(
        WindowsCrashIngestOptions options,
        out Mock<IWindowsCrashIngestService> ingest,
        ICrashIngestRateLimiter? rateLimiter = null)
    {
        ingest = new Mock<IWindowsCrashIngestService>();
        var metrics = new Mock<ICrashIngestMetrics>();
        var logger = new Mock<ILogger<WindowsCrashIngestController>>();
        var limiter = rateLimiter ?? CreatePermissiveRateLimiter();
        return new WindowsCrashIngestController(
            ingest.Object,
            limiter,
            metrics.Object,
            Options.Create(options),
            logger.Object);
    }

    private static ICrashIngestRateLimiter CreatePermissiveRateLimiter()
    {
        var mock = new Mock<ICrashIngestRateLimiter>();
        mock.Setup(r => r.TryConsume(It.IsAny<string>(), out It.Ref<int>.IsAny)).Returns(true);
        return mock.Object;
    }

    [Fact]
    public async Task Ingest_WithValidPayload_ReturnsNoContentAndCallsService()
    {
        var options = new WindowsCrashIngestOptions { RequireHttps = true, MaxPayloadBytes = 1024 };
        var controller = CreateController(options, out var ingest);
        var payload = """
                      process=com.imkolganov.datagate.win
                      kind=fatal

                      java.lang.RuntimeException: boom
                      """;
        AttachHttpsRequest(controller, payload);

        var result = await controller.Ingest(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        ingest.Verify(
            s => s.SaveAsync("com.imkolganov.datagate.win", "win_crash_2026-05-01T00-00-00.000Z.txt", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ingest_WithEmptyBody_ReturnsBadRequest()
    {
        var controller = CreateController(new WindowsCrashIngestOptions(), out _);
        AttachHttpsRequest(controller, string.Empty);

        var result = await controller.Ingest(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Ingest_WhenRateLimitExceeded_ReturnsTooManyRequests()
    {
        var options = new WindowsCrashIngestOptions
        {
            RateLimitMaxRequests = 1,
            RateLimitWindowSeconds = 300,
        };
        var crashOptions = new CrashIngestOptions
        {
            RateLimitMaxRequests = options.RateLimitMaxRequests,
            RateLimitWindowSeconds = options.RateLimitWindowSeconds,
        };
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var rateLimiter = new MemoryCrashIngestRateLimiter(cache, Options.Create(crashOptions));
        var controller = CreateController(options, out _, rateLimiter);

        var payload = """
                      process=com.imkolganov.datagate.win

                      stacktrace
                      """;
        AttachHttpsRequest(controller, payload);
        var first = await controller.Ingest(CancellationToken.None);
        Assert.IsType<NoContentResult>(first);

        AttachHttpsRequest(controller, payload);
        var second = await controller.Ingest(CancellationToken.None);
        var status = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, status.StatusCode);
    }

    [Fact]
    public async Task Ingest_WhenPayloadTooLarge_ReturnsPayloadTooLarge()
    {
        var controller = CreateController(new WindowsCrashIngestOptions { MaxPayloadBytes = 32 }, out _);
        AttachHttpsRequest(controller, new string('A', 64));

        var result = await controller.Ingest(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
    }

    [Fact]
    public async Task Recent_WhenLimitNonPositive_ReturnsBadRequest()
    {
        var controller = CreateController(new WindowsCrashIngestOptions { RecentMaxLimit = 100 }, out _);
        var result = await controller.Recent(0, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Recent_ClampsLimitAndReturnsServiceResult()
    {
        var controller = CreateController(new WindowsCrashIngestOptions { RecentMaxLimit = 10 }, out var ingest);
        ingest.Setup(s => s.GetRecentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DataGateMonitor.SharedModels.DataGateMonitor.WindowsCrashIngest.Dto.RecentWindowsCrashReportDto
                {
                    Id = 2,
                    AppProcess = "com.example.win",
                    FileName = "w.txt"
                }
            ]);

        var result = await controller.Recent(500, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<DataGateMonitor.SharedModels.DataGateMonitor.WindowsCrashIngest.Dto.RecentWindowsCrashReportDto>>(ok.Value);
        Assert.Single(list);
        ingest.Verify(s => s.GetRecentAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ingest_WhenHttpsRequiredOverHttp_ReturnsBadRequest()
    {
        var controller = CreateController(new WindowsCrashIngestOptions { RequireHttps = true }, out _);
        var context = new DefaultHttpContext
        {
            Request =
            {
                Scheme = "http",
                ContentType = "text/plain; charset=utf-8",
                Body = new MemoryStream(Encoding.UTF8.GetBytes("x")),
            },
        };
        context.Request.Headers["X-Crash-Filename"] = "f.txt";
        context.Request.Headers["X-Crash-Process"] = "com.imkolganov.datagate.win";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Ingest(CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
