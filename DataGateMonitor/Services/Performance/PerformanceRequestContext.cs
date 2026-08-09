namespace DataGateMonitor.Services.Performance;

/// <summary>
/// Correlates EF command samples with the ambient HTTP request (set by timing middleware).
/// </summary>
public static class PerformanceRequestContext
{
    private static readonly AsyncLocal<string?> CurrentRequestId = new();

    public static string? RequestId
    {
        get => CurrentRequestId.Value;
        set => CurrentRequestId.Value = value;
    }
}
