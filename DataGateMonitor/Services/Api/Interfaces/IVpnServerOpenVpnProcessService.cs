using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OpenVpnProcess.Responses;

namespace DataGateMonitor.Services.Api.Interfaces;

public interface IVpnServerOpenVpnProcessService
{
    Task<OpenVpnProcessStatusResponse> GetStatusAsync(int vpnServerId, CancellationToken ct);
    Task<OpenVpnProcessStatusResponse> StartAsync(int vpnServerId, CancellationToken ct);
    Task<OpenVpnProcessStatusResponse> RestartAsync(int vpnServerId, CancellationToken ct);
    Task<OpenVpnProcessStatusResponse> KillAsync(int vpnServerId, CancellationToken ct);
}
