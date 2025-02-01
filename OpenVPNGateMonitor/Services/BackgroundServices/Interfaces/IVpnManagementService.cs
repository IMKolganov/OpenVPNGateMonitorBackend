using OpenVPNGateMonitor.Models.Helpers.OpenVpnManagementInterfaces;

namespace OpenVPNGateMonitor.Services.BackgroundServices.Interfaces;

public interface IVpnManagementService
{
    Task<string> GetVersionAsync();
    Task<OpenVpnState> GetStateAsync();
    Task<OpenVpnStats> GetStatsAsync();
    Task<List<OpenVpnClient>> GetClientsAsync();
}