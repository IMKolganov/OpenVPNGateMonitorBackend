using DataGateMonitor.Services.VpnManagerReleases;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;

namespace DataGateMonitor.Tests.Services.VpnManagerReleases;

internal sealed class NoOpVpnManagerUpdateStatusEnricher : IVpnManagerUpdateStatusEnricher
{
    public Task EnrichAsync(IList<VpnServerWithStatusDto> items, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task EnrichAsync(IList<VpnServerWithStatusV2Dto> items, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
