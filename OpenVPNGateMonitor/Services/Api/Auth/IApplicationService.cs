using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api.Auth;

public interface IApplicationService
{
    Task<RegisteredApp> RegisterApplicationAsync(string name);
    Task<RegisteredApp?> GetApplicationByClientIdAsync(string clientId);
    Task<RegisteredApp?> GetApplicationSystemByClientIdAsync(string clientId);
    Task<bool> IsSystemApplicationSetAsync();
    Task<List<RegisteredApp>> GetAllApplicationsAsync();
    Task<RegisteredApp> UpdateApplicationAsync(RegisteredApp registeredApp);
    Task<bool> RevokeApplicationAsync(string clientId);
}