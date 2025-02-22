using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api.Auth;

public interface IApplicationService
{
    Task<RegisteredApp> RegisterApplicationAsync(string name);
    Task<RegisteredApp?> GetApplicationByClientIdAsync(string clientId);
    Task<List<RegisteredApp>> GetAllApplicationsAsync();
    Task<bool> RevokeApplicationAsync(string clientId);
}