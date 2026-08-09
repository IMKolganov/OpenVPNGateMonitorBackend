namespace DataGateMonitor.Services.Performance;

public interface IPerformanceSampleStore
{
    Task AppendHttpAsync(PerformanceHttpSample sample, CancellationToken ct = default);

    Task AppendDbAsync(PerformanceDbSample sample, CancellationToken ct = default);

    Task<IReadOnlyList<PerformanceHttpSample>> GetHttpLatestAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<PerformanceDbSample>> GetDbLatestAsync(int limit, CancellationToken ct = default);

    Task ClearHttpAsync(CancellationToken ct = default);

    Task ClearDbAsync(CancellationToken ct = default);

    Task ClearAllAsync(CancellationToken ct = default);
}
