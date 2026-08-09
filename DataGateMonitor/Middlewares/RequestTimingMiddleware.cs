using System.Diagnostics;
using System.Security.Claims;
using DataGateMonitor.Services.Performance;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Middlewares;

public sealed class RequestTimingMiddleware(
    RequestDelegate next,
    IPerformanceSampleStore sampleStore,
    IOptions<PerformanceMonitoringOptions> options,
    ILogger<RequestTimingMiddleware> logger)
{
    private static readonly PathString[] SkipPrefixes =
    [
        new("/api/performance"),
        new("/health"),
        new("/swagger"),
        new("/api/hubs"),
        new("/hubs")
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context.Request))
        {
            await next(context);
            return;
        }

        var requestId = Guid.NewGuid().ToString("N");
        PerformanceRequestContext.RequestId = requestId;
        context.Items["PerformanceRequestId"] = requestId;

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            try
            {
                await MaybeRecordAsync(context, requestId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to record HTTP performance sample.");
            }
            finally
            {
                PerformanceRequestContext.RequestId = null;
            }
        }
    }

    private async Task MaybeRecordAsync(HttpContext context, string requestId, long durationMs)
    {
        var statusCode = context.Response.StatusCode;
        var threshold = Math.Max(0, options.Value.HttpSlowMs);
        if (durationMs < threshold && statusCode < 500)
            return;

        var userName =
            context.User?.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name
                  ?? context.User.FindFirstValue(ClaimTypes.Name)
                  ?? context.User.FindFirstValue(ClaimTypes.Email)
                : null;

        var path = context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "/";
        if (context.Request.QueryString.HasValue)
            path += context.Request.QueryString.Value;

        await sampleStore.AppendHttpAsync(
            new PerformanceHttpSample
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                RequestId = requestId,
                Method = context.Request.Method,
                Path = path,
                StatusCode = statusCode,
                DurationMs = durationMs,
                UserName = userName,
                TraceId = context.TraceIdentifier
            },
            context.RequestAborted);
    }

    private static bool ShouldSkip(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method))
            return true;

        var path = request.Path;
        if (!path.HasValue)
            return true;

        // Skip non-API static / root assets; still time /api/* (except exclusions).
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var prefix in SkipPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
