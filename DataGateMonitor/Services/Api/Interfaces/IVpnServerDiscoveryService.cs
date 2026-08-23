using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Dto;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.VpnServers.Responses;

namespace DataGateMonitor.Services.Api.Interfaces;

public interface IVpnServerDiscoveryService
{
    bool TryAcquireAnnounceSlot(string? clientIp);

    Task<AnnounceVpnServerResponse> AnnounceAsync(AnnounceVpnServerRequest request, CancellationToken ct);

    Task<VpnServerDiscoveriesResponse> ListPendingAsync(CancellationToken ct);

    Task<VpnServerDiscoveryResponse> ApproveAsync(int discoveryId, ApproveVpnServerDiscoveryRequest request, CancellationToken ct);

    Task<VpnServerDiscoveryResponse> DenyAsync(int discoveryId, DenyVpnServerDiscoveryRequest? request, CancellationToken ct);
}
