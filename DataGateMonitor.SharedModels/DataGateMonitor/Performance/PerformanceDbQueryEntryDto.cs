namespace DataGateMonitor.SharedModels.DataGateMonitor.Performance;

public class PerformanceDbQueryEntryDto
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string? RequestId { get; set; }

    public long DurationMs { get; set; }

    public string CommandType { get; set; } = string.Empty;

    public string Sql { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public string? ExceptionMessage { get; set; }
}
