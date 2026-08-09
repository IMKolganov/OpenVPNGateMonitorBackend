namespace DataGateMonitor.SharedModels.DataGateMonitor.Performance;

public class PerformanceHttpRequestEntryDto
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
