using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Requests;
using DataGateMonitor.Services.Api.Auth.Registers;

namespace DataGateMonitor.Services.Api.Auth.Registers.Interfaces;

public interface IApplicationService
{
    Task<RegisteredClientApplication> RegisterApplicationAsync(string name, CancellationToken cancellationToken);
    Task<ClientApplication?> GetApplicationByClientIdAsync(string clientId, CancellationToken cancellationToken);
    Task<ClientApplication?> GetApplicationSystemByClientIdAsync(string clientId, CancellationToken cancellationToken);
    Task<bool> IsSystemApplicationSetAsync(CancellationToken cancellationToken);
    Task<List<ClientApplication>> GetAllApplicationsAsync(CancellationToken cancellationToken);
    Task<List<ClientApplication>> GetAllApplicationsAsync(GetAllApplicationsRequest request, CancellationToken cancellationToken);
    Task<ClientApplication> UpdateApplicationAsync(ClientApplication clientApplication,
        CancellationToken cancellationToken);
    Task<bool> RevokeApplicationAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates client credentials. Returns the app when valid and not revoked; otherwise null.
    /// On successful legacy (plaintext) verification, upgrades the stored secret to bcrypt.
    /// </summary>
    Task<ClientApplication?> AuthenticateClientAsync(string clientId, string clientSecret, CancellationToken cancellationToken);
}
