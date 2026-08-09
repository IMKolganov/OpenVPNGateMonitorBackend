using DataGateMonitor.Data.Interceptors;
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
    public async Task Invoke_DoesNotRecord_Fast_Successful_Request()
    {
        var store = new Mock<IPerformanceSampleStore>(MockBehavior.Strict);
        var middleware = new RequestTimingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 10_000 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/servers";

        await middleware.InvokeAsync(context);

        store.Verify(
            s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        foreach (var path in new[] { "/api/performance/http-requests", "/health", "/swagger/index.html", "/api/hubs/status-stream" })
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
    public async Task Invoke_Skips_Options_And_NonApi_Paths()
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

        var optionsCtx = new DefaultHttpContext();
        optionsCtx.Request.Method = "OPTIONS";
        optionsCtx.Request.Path = "/api/servers";
        await middleware.InvokeAsync(optionsCtx);

        var staticCtx = new DefaultHttpContext();
        staticCtx.Request.Method = "GET";
        staticCtx.Request.Path = "/favicon.ico";
        await middleware.InvokeAsync(staticCtx);

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

    [Fact]
    public async Task Invoke_Sets_And_Clears_PerformanceRequestContext()
    {
        string? midRequestId = null;
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new RequestTimingMiddleware(
            ctx =>
            {
                midRequestId = PerformanceRequestContext.RequestId;
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 0 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        PerformanceRequestContext.RequestId = null;
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/servers";

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(midRequestId));
        Assert.Null(PerformanceRequestContext.RequestId);
        Assert.Equal(midRequestId, context.Items["PerformanceRequestId"]);
    }

    [Fact]
    public async Task Invoke_Clears_RequestId_Even_When_Next_Throws()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new RequestTimingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 0 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        PerformanceRequestContext.RequestId = null;
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/servers";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Null(PerformanceRequestContext.RequestId);
    }

    [Fact]
    public async Task Invoke_DoesNotFail_When_Store_Throws()
    {
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

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
        context.Request.Method = "GET";
        context.Request.Path = "/api/servers";

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(ex);
        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_Correlates_RequestId_With_Db_Interceptor_Sample()
    {
        PerformanceHttpSample? httpSample = null;
        var tcs = new TaskCompletionSource<PerformanceDbSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendHttpAsync(It.IsAny<PerformanceHttpSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceHttpSample, CancellationToken>((s, _) => httpSample = s)
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendDbAsync(It.IsAny<PerformanceDbSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceDbSample, CancellationToken>((s, _) => tcs.TrySetResult(s))
            .Returns(Task.CompletedTask);

        var interceptor = new SlowDbCommandInterceptor(
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { DbSlowMs = 1 }),
            NullLogger<SlowDbCommandInterceptor>.Instance);

        var middleware = new RequestTimingMiddleware(
            ctx =>
            {
                interceptor.Record(
                    new CorrelationDbCommand("SELECT correlated"),
                    TimeSpan.FromMilliseconds(50),
                    succeeded: true,
                    exception: null);
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { HttpSlowMs = 0 }),
            NullLogger<RequestTimingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/open-vpn-servers";

        await middleware.InvokeAsync(context);

        var dbSample = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(httpSample);
        Assert.Equal(httpSample!.RequestId, dbSample.RequestId);
        Assert.Null(PerformanceRequestContext.RequestId);
    }

    private sealed class CorrelationDbCommand(string commandText) : System.Data.Common.DbCommand
    {
        public override string CommandText { get; set; } = commandText;
        public override int CommandTimeout { get; set; }
        public override System.Data.CommandType CommandType { get; set; } = System.Data.CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
        protected override System.Data.Common.DbConnection? DbConnection { get; set; }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } =
            new EmptyParameterCollection();
        protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare()
        {
        }

        protected override System.Data.Common.DbParameter CreateDbParameter() =>
            throw new NotSupportedException();

        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) =>
            throw new NotSupportedException();

        private sealed class EmptyParameterCollection : System.Data.Common.DbParameterCollection
        {
            public override int Count => 0;
            public override object SyncRoot { get; } = new();
            public override int Add(object value) => 0;
            public override void AddRange(Array values)
            {
            }

            public override void Clear()
            {
            }

            public override bool Contains(object value) => false;
            public override bool Contains(string value) => false;
            public override void CopyTo(Array array, int index)
            {
            }

            public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
            public override int IndexOf(object value) => -1;
            public override int IndexOf(string parameterName) => -1;
            public override void Insert(int index, object value)
            {
            }

            public override void Remove(object value)
            {
            }

            public override void RemoveAt(int index)
            {
            }

            public override void RemoveAt(string parameterName)
            {
            }

            protected override System.Data.Common.DbParameter GetParameter(int index) =>
                throw new NotSupportedException();

            protected override System.Data.Common.DbParameter GetParameter(string parameterName) =>
                throw new NotSupportedException();

            protected override void SetParameter(int index, System.Data.Common.DbParameter value)
            {
            }

            protected override void SetParameter(string parameterName, System.Data.Common.DbParameter value)
            {
            }
        }
    }
}
