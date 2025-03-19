using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api.Auth;

public interface IApplicationService
{
    Task<ClientApplication> RegisterApplicationAsync(string name);
    Task<ClientApplication?> GetApplicationByClientIdAsync(string clientId);
    Task<ClientApplication?> GetApplicationSystemByClientIdAsync(string clientId);
    Task<bool> IsSystemApplicationSetAsync();
    Task<List<ClientApplication>> GetAllApplicationsAsync();
    Task<ClientApplication> UpdateApplicationAsync(ClientApplication clientApplication);
    Task<bool> RevokeApplicationAsync(string clientId);
}