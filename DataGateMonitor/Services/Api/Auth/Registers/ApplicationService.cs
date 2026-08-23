using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.ClientApplicationTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Registers;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.Applications.Requests;

namespace DataGateMonitor.Services.Api.Auth;

public class ApplicationService(
    IClientApplicationQueryService clientApplicationQueryService,
    ICommandService<ClientApplication, int> clientApplicationCommandService
    ) : IApplicationService
{
    public const int MaxClientNameLength = 128;

    public async Task<RegisteredClientApplication> RegisterApplicationAsync(string name, CancellationToken ct)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("API client name is required.");
        if (normalized.Length > MaxClientNameLength)
            throw new ArgumentException($"API client name must be at most {MaxClientNameLength} characters.");

        var existClientApplication = await clientApplicationQueryService.GetByName(normalized, ct);

        if (existClientApplication != null)
        {
            throw new InvalidOperationException("An API client with this name already exists.");
        }

        var plaintextSecret = Guid.NewGuid().ToString("N");
        var clientApplication = new ClientApplication
        {
            Name = normalized,
            ClientSecret = ClientApplicationSecret.Hash(plaintextSecret),
        };

        await clientApplicationCommandService.Add(clientApplication, true, ct);

        return new RegisteredClientApplication
        {
            Application = clientApplication,
            PlaintextClientSecret = plaintextSecret,
        };
    }

    public async Task<ClientApplication?> GetApplicationByClientIdAsync(string clientId,
        CancellationToken ct)
    {
        return await clientApplicationQueryService.GetByClientId(clientId, ct);
    }

    public async Task<ClientApplication?> GetApplicationSystemByClientIdAsync(string clientId,
        CancellationToken ct)
    {
        return await clientApplicationQueryService.GetBySystemByClientId(clientId, ct);
    }

    public async Task<bool> IsSystemApplicationSetAsync(CancellationToken ct)
    {
        var systemApp = await clientApplicationQueryService.IsSystemConfigured(ct);

        return systemApp != null && !string.IsNullOrEmpty(systemApp.ClientSecret);
    }

    public Task<List<ClientApplication>> GetAllApplicationsAsync(CancellationToken ct)
        => GetAllApplicationsAsync(new GetAllApplicationsRequest(), ct);

    public Task<List<ClientApplication>> GetAllApplicationsAsync(GetAllApplicationsRequest request, CancellationToken ct)
        => clientApplicationQueryService.GetFiltered(request, ct);

    public async Task<ClientApplication> UpdateApplicationAsync(ClientApplication clientApplication,
        CancellationToken ct)
    {
        await clientApplicationCommandService.Update(clientApplication, true, ct);

        return clientApplication;
    }

    public async Task<bool> RevokeApplicationAsync(string clientId, CancellationToken ct)
    {
        var clientApplication = await clientApplicationQueryService.GetByClientId(clientId, ct);

        if (clientApplication == null)
            return false;

        if (clientApplication.IsSystem)
            throw new InvalidOperationException("System API clients cannot be revoked.");

        if (clientApplication.IsRevoked)
            return true;

        clientApplication.IsRevoked = true;

        await clientApplicationCommandService.Update(clientApplication, true, ct);
        return true;
    }

    public async Task<ClientApplication?> AuthenticateClientAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret))
            return null;

        var app = await clientApplicationQueryService.GetByClientId(clientId.Trim(), cancellationToken);
        if (app == null || app.IsRevoked)
            return null;

        if (!ClientApplicationSecret.Verify(clientSecret, app.ClientSecret))
            return null;

        // Dual-write upgrade: legacy plaintext → bcrypt after successful auth.
        if (!ClientApplicationSecret.LooksLikeBcrypt(app.ClientSecret))
        {
            app.ClientSecret = ClientApplicationSecret.Hash(clientSecret);
            await clientApplicationCommandService.Update(app, true, cancellationToken);
        }

        return app;
    }
}
