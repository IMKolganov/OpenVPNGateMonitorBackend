using DataGateMonitor.Middlewares;
using DataGateMonitor.Services.Performance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Middlewares;

public class RequestTimingMiddlewareTests
{
    [Fact]
    public async Task Invoke_Records_When_Duration_Exceeds_Threshold()
    {
        PerformanceHttpSample? recorded = null;
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceHttpSample, CancellationToken>((s, _) => recorded = s)
            .Returns(Task.CompletedTask);

        var middleware = new RequestTimingMiddleware(
            async ctx =>
            {
                await Task.Delay(30);
                ctx.Response.StatusCode = 200;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 10 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/servers";

        await middleware.InvokeAsync(context);

        Assert.NotNull(recorded);
        Assert.Equal("GET", recorded!.Method);
        Assert.Equal("/api/servers", recorded.Path);
        Assert.True(recorded.DurationMs >= 10);
        Assert.False(string.IsNullOrWhiteSpace(recorded.RequestId));
    }

    [Fact]
    public async Task Invoke_Skips_Performance_And_Health_Paths()
    {
        var store = new Mock<IPerformanceSampleStore>(MockBehavior.Strict);
        var middleware = new RequestTimingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 0 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        foreach (var path in new[] { "/api/performance/http-requests", "/health", "/swagger/index.html" })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = path;
            await middleware.InvokeAsync(context);
        }

        store.Verify(
            s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Invoke_Records_5xx_Even_When_Fast()
    {
        PerformanceHttpSample? recorded = null;
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceHttpSample, CancellationToken>((s, _) => recorded = s)
            .Returns(Task.CompletedTask);

        var middleware = new RequestTimingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 500;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 10_000 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/settings";

        await middleware.InvokeAsync(context);

        Assert.NotNull(recorded);
        Assert.Equal(500, recorded!.StatusCode);
    }
}
