namespace DataGateMonitor.Services.Performance;

public sealed class PerformanceHttpSample
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public long DurationMs { get; set; }

    public string? UserName { get; set; }

    public string? TraceId { get; set; }
}

public sealed class PerformanceDbSample
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string? RequestId { get; set; }

    public long DurationMs { get; set; }

    public string CommandType { get; set; } = string.Empty;

    public string Sql { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public string? ExceptionMessage { get; set; }
}
