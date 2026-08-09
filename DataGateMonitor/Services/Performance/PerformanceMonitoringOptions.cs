namespace DataGateMonitor.Services.Performance;

public sealed class PerformanceMonitoringOptions
{
    public const string SectionName = "PerformanceMonitoring";

    /// <summary>HTTP samples at or above this duration (ms) are retained. Default 200.</summary>
    public int HttpSlowMs { get; set; } = 200;

    /// <summary>DB samples at or above this duration (ms) are retained. Default 100.</summary>
    public int DbSlowMs { get; set; } = 100;

    /// <summary>Max entries per ring (HTTP / DB). Default 500, capped at 2000.</summary>
    public int MaxEntries { get; set; } = 500;

    /// <summary>Max SQL characters stored (no parameter values). Default 2000.</summary>
    public int SqlMaxLength { get; set; } = 2000;
}
