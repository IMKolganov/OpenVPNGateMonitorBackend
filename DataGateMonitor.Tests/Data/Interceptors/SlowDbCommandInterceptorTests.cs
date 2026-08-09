using System.Data;
using System.Data.Common;
using DataGateMonitor.Data.Interceptors;
using DataGateMonitor.Services.Performance;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Data.Interceptors;

public class SlowDbCommandInterceptorTests
{
    [Fact]
    public async Task Record_When_Slow_Appends_Sample_With_RequestId_And_Truncated_Sql()
    {
        var tcs = new TaskCompletionSource<PerformanceDbSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendDbAsync(It.IsAny<PerformanceDbSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceDbSample, CancellationToken>((s, _) => tcs.TrySetResult(s))
            .Returns(Task.CompletedTask);

        var interceptor = new SlowDbCommandInterceptor(
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { DbSlowMs = 50, SqlMaxLength = 64 }),
            NullLogger<SlowDbCommandInterceptor>.Instance);

        PerformanceRequestContext.RequestId = "req-1";
        try
        {
            var command = new FakeDbCommand(new string('x', 120));
            interceptor.Record(command, TimeSpan.FromMilliseconds(120), succeeded: true, exception: null);

            var recorded = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("req-1", recorded.RequestId);
            Assert.True(recorded.Succeeded);
            Assert.Equal(120, recorded.DurationMs);
            Assert.EndsWith("…", recorded.Sql);
            Assert.Equal(65, recorded.Sql.Length);
        }
        finally
        {
            PerformanceRequestContext.RequestId = null;
        }
    }

    [Fact]
    public async Task Record_When_Failed_Appends_Even_If_Fast()
    {
        var tcs = new TaskCompletionSource<PerformanceDbSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Mock<IPerformanceSampleStore>();
        store.Setup(s => s.AppendDbAsync(It.IsAny<PerformanceDbSample>(), It.IsAny<CancellationToken>()))
            .Callback<PerformanceDbSample, CancellationToken>((s, _) => tcs.TrySetResult(s))
            .Returns(Task.CompletedTask);

        var interceptor = new SlowDbCommandInterceptor(
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { DbSlowMs = 10_000 }),
            NullLogger<SlowDbCommandInterceptor>.Instance);

        interceptor.Record(
            new FakeDbCommand("UPDATE x"),
            TimeSpan.FromMilliseconds(1),
            succeeded: false,
            exception: new InvalidOperationException("boom"));

        var recorded = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(recorded.Succeeded);
        Assert.Equal("boom", recorded.ExceptionMessage);
    }

    [Fact]
    public void Record_DoesNotAppend_When_Fast_And_Succeeded()
    {
        var store = new Mock<IPerformanceSampleStore>(MockBehavior.Strict);
        var interceptor = new SlowDbCommandInterceptor(
            store.Object,
            Options.Create(new PerformanceMonitoringOptions { DbSlowMs = 100 }),
            NullLogger<SlowDbCommandInterceptor>.Instance);

        interceptor.Record(
            new FakeDbCommand("SELECT 1"),
            TimeSpan.FromMilliseconds(5),
            succeeded: true,
            exception: null);

        store.Verify(
            s => s.AppendDbAsync(It.IsAny<PerformanceDbSample>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, 10, "")]
    [InlineData("abc", 10, "abc")]
    [InlineData("abcdefghij", 5, "abcde…")]
    public void Truncate_Respects_Max(string? input, int max, string expected)
    {
        Assert.Equal(expected, SlowDbCommandInterceptor.Truncate(input, max));
    }

    private sealed class FakeDbCommand(string commandText) : DbCommand
    {
        public override string CommandText { get; set; } = commandText;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType()
        {
        }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
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

        protected override DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value)
        {
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
        }
    }
}
